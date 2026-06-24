using EggLedger.Web.Server.Sync.Auth;
using EggLedger.Web.Server.Sync.Blobs;
using EggLedger.Web.Server.Sync.Db;
using EggLedger.Web.Server.Sync.Menno;
using EggLedger.Web.Server.Sync.Verify;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Synckit.Auth;
using Synckit.Contract;

namespace EggLedger.Web.Server.Sync;

public static class Api {
    public static void Map(WebApplication app, AppConfig cfg, VerifyInfo build) {
        var source = app.Services.GetService<NpgsqlDataSource>()
                     ?? NpgsqlDataSource.Create(cfg.DatabaseUrl);

        var auth = new AuthEndpoints(source);
        var blobs = new BlobEndpoints(source);
        var menno = new MennoEndpoint(new HttpClient(), cfg.MennoFunctionKey, AppConfig.MennoUpstreamUrl);
        var store = new SessionStore(source);
        var metrics = new Admin.ApiMetrics(TimeProvider.System);
        var admin = new Admin.AdminEndpoints(source, metrics, cfg.AdminDiscordIds);

        // Count every /api/v1 request for the admin traffic charts.
        app.Use(async (ctx, next) => {
            if (ctx.Request.Path.StartsWithSegments("/api/v1")) {
                metrics.Record(ctx.Request.Path.Value ?? "");
            }
            await next();
        });

        // public
        app.MapGet("/api/v1/auth/discord", (HttpContext c) => auth.Discord(c));
        app.MapGet("/api/v1/auth/callback", (HttpContext c) => auth.Callback(c));
        app.MapGet("/api/v1/auth/poll", (HttpContext c) => auth.Poll(c, c.Request.Query["state"].ToString()));
        app.MapDelete("/api/v1/auth/session", (HttpContext c) => auth.DeleteSession(c));

        VerifyEndpoint.Map(app, build);
        app.MapPost("/api/v1/menno/submit", (HttpContext c) => menno.Submit(c));

        // authed: wrap each handler in RequireAuth
        MapAuthed(app, ["PUT"], "/api/v1/blobs/{name}", store,
            c => blobs.Put(c, (string)c.Request.RouteValues["name"]!));
        MapAuthed(app, ["GET"], "/api/v1/blobs/{name}", store,
            c => blobs.Get(c, (string)c.Request.RouteValues["name"]!));
        MapAuthed(app, ["GET"], "/api/v1/blobs", store,
            c => blobs.List(c));
        MapAuthed(app, ["DELETE"], "/api/v1/blobs/{name}", store,
            c => blobs.Delete(c, (string)c.Request.RouteValues["name"]!));
        MapAuthed(app, ["DELETE"], "/api/v1/user", store,
            c => blobs.DeleteUser(c));

        // Admin: authed + allowlist-gated inside each handler.
        MapAuthed(app, ["GET"], "/api/v1/admin/me", store, admin.Me);
        MapAuthed(app, ["GET"], "/api/v1/admin/users", store, admin.Users);
        MapAuthed(app, ["GET"], "/api/v1/admin/metrics", store, admin.Metrics);
        MapAuthed(app, ["DELETE"], "/api/v1/admin/users/{discordId}", store,
            c => admin.DeleteUser(c, (string)c.Request.RouteValues["discordId"]!));
    }

    private static void MapAuthed(WebApplication app, string[] methods, string pattern,
        ISessionStore store, RequestDelegate handler) {
        Task Pipeline(HttpContext ctx) => new RequireAuth(handler, store).Invoke(ctx);
        app.MapMethods(pattern, methods, (RequestDelegate)Pipeline);
    }
}
