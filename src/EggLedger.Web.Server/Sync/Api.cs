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
using SyncKit.Auth;
using SyncKit.Contract;

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
        var spam = new Admin.SpamLog(source);
        var admin = new Admin.AdminEndpoints(source, metrics, spam, cfg.AdminDiscordIds);

        // Routing must run before the metrics middleware so ctx.GetEndpoint() reflects
        // whether a route matched. Valid /api/v1 hits are count-only; unmatched ones (404
        // probes) get the client logged to el_api_spam for the admin spam view.
        app.UseRouting();
        app.Use(async (ctx, next) => {
            if (ctx.Request.Path.StartsWithSegments("/api/v1")) {
                if (ctx.GetEndpoint() is not null) {
                    metrics.Record(ctx.Request.Path.Value ?? "");
                } else {
                    var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    var ua = ctx.Request.Headers.UserAgent.ToString();
                    var now = TimeProvider.System.GetUtcNow().ToUnixTimeSeconds();
                    // Logging the probe must never break the response.
                    _ = Task.Run(async () => {
                        try {
                            await spam.RecordAsync(ip, ctx.Request.Method, ctx.Request.Path.Value ?? "", ua, now);
                        } catch { }
                    });
                }
            }
            await next();
        });

        // public
        app.MapGet("/api/v1/auth/discord", (HttpContext c) => auth.Discord(c));
        app.MapGet("/api/v1/auth/login", (HttpContext c) => auth.Login(c));
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

        // Ship 3D assets: authed + admin-allowlist gated. Bytes live in a server-local dir
        // (not wwwroot), served only here, until licensing is confirmed.
        var ships = app.Services.GetRequiredService<Ships.ShipAssetService>();
        var adminIds = cfg.AdminDiscordIds;
        MapAuthed(app, ["GET"], "/api/ships/manifest", store, async ctx => {
            if (!adminIds.Contains(ctx.Request.Headers["X-Discord-ID"].ToString())) {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            ctx.Response.ContentType = "application/json; charset=utf-8";
            await ctx.Response.WriteAsJsonAsync(
                ships.Manifest.Ships.ToDictionary(kv => kv.Key, kv => new { bbox = kv.Value.Bbox }),
                ctx.RequestAborted);
        });
        MapAuthed(app, ["GET"], "/api/ships/{key}.glb", store, async ctx => {
            bool isAdmin = adminIds.Contains(ctx.Request.Headers["X-Discord-ID"].ToString());
            var key = (string)ctx.Request.RouteValues["key"]!;
            var r = await Ships.ShipEndpoints.HandleGlb(ships, isAdmin, key, ctx.RequestAborted);
            ctx.Response.StatusCode = r.Status;
            if (r.Bytes is not null) {
                ctx.Response.ContentType = r.ContentType!;
                ctx.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
                await ctx.Response.Body.WriteAsync(r.Bytes, ctx.RequestAborted);
            }
        });
    }

    private static void MapAuthed(WebApplication app, string[] methods, string pattern,
        ISessionStore store, RequestDelegate handler) {
        Task Pipeline(HttpContext ctx) => new RequireAuth(handler, store).Invoke(ctx);
        app.MapMethods(pattern, methods, (RequestDelegate)Pipeline);
    }
}
