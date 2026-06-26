using EggLedger.Web;
using EggLedger.Web.Data;
using EggLedger.Web.Server.Components;
using EggLedger.Web.Server.Sync;
using EggLedger.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using SyncKit.Auth;
using SyncKit.Bot;
using SyncKit.Contract;
using SyncKit.Db;

var builder = WebApplication.CreateBuilder(args);

// Folded-in sync server (Phase 2). Reads env config; wires /api/v1/* onto this same
// host. When DATABASE_URL is unset (dev), sync wiring is skipped and only the UI boots.
var cfg = AppConfig.FromEnv(Environment.GetEnvironmentVariable);
var hasDb = !string.IsNullOrEmpty(cfg.DatabaseUrl);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(o => o.DetailedErrors = true);

// Behind nginx TLS termination: trust the forwarded proto/host so the app knows it is
// HTTPS (secure cookies + correct OAuth redirect scheme).
builder.Services.Configure<ForwardedHeadersOptions>(o => {
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

// Cookie auth carries the Discord identity into the Blazor circuit via the framework
// AuthenticationStateProvider (the OAuth callback signs in; the circuit reads claims).
builder.Services.AddAuthentication(EggLedger.Web.Server.Auth.AuthScheme.Cookie)
    .AddCookie(EggLedger.Web.Server.Auth.AuthScheme.Cookie, o => {
        o.Cookie.Name = "el_session";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        o.ExpireTimeSpan = TimeSpan.FromDays(30);
        o.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

if (hasDb) {
    var dataSource = NpgsqlDataSource.Create(cfg.DatabaseUrl);
    builder.Services.AddSingleton(dataSource);

    // Persist the DataProtection key ring to Postgres so cookie-auth keys survive
    // container redeploys (otherwise every deploy invalidates all sessions).
    builder.Services.AddDataProtection()
        .SetApplicationName("EggLedger")
        .AddKeyManagementOptions(o =>
            o.XmlRepository = new EggLedger.Web.Server.Auth.PostgresXmlRepository(dataSource));
}

// Self-origin base for the shared UI's HttpClient (in-process /api/v1 + /egg-api calls).
// Defaults to the bound listen address with 0.0.0.0/[::] rewritten to localhost.
var selfBase = new Uri(builder.Configuration["SelfBaseAddress"] ?? SelfBaseFromUrls());
builder.Services.AddEggLedgerWeb(selfBase);

static string SelfBaseFromUrls() {
    var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
    var first = urls?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
    if (string.IsNullOrEmpty(first)) {
        return "http://localhost:5015";
    }
    return first.Replace("0.0.0.0", "localhost").Replace("[::]", "localhost").Replace("+", "localhost");
}

// Override the WASM blob-crypto seam: WASM used SubtleCrypto, the server uses managed
// AES-GCM. Decode needs no override (LocalApiPayloadDecoder is already the default).
builder.Services.RemoveAll<IBlobCipher>();
builder.Services.AddScoped<IBlobCipher, LocalBlobCipher>();

// Server-side per-user storage, only when a DB is configured (dev without DB keeps the
// browser IndexedDB path). PostgresIndexedDb scopes every op to the circuit's CurrentUser.
builder.Services.AddScoped<EggLedger.Web.Server.Storage.CurrentUser>();
if (hasDb) {
    builder.Services.AddScoped<ISessionStore>(sp =>
        new EggLedger.Web.Server.Sync.Db.SessionStore(sp.GetRequiredService<NpgsqlDataSource>()));
    builder.Services.RemoveAll<IIndexedDb>();
    builder.Services.AddScoped<IIndexedDb>(sp => new EggLedger.Web.Server.Storage.PostgresIndexedDb(
        sp.GetRequiredService<NpgsqlDataSource>(),
        sp.GetRequiredService<EggLedger.Web.Server.Storage.CurrentUser>()));
    builder.Services.AddScoped<EggLedger.Web.Server.Storage.FirstLoginBackfill>();
}

// Outbound client for the egg-api forward (server calls auxbrain directly; no browser CORS).
builder.Services.AddHttpClient("auxbrain", c => c.BaseAddress = new Uri("https://www.auxbrain.com"));

var app = builder.Build();

app.UseForwardedHeaders();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
// Required: MapRazorComponents marks endpoints with anti-forgery metadata, so the
// middleware must be present. Keys persist to Postgres for a stable key ring.
app.UseAntiforgery();

// Reverse-proxy the egg-api prefix to auxbrain server-side (replaces the nginx CORS dodge).
app.Map("/egg-api/{**rest}", async (HttpContext ctx, IHttpClientFactory factory, string rest) => {
    var client = factory.CreateClient("auxbrain");
    using var req = new HttpRequestMessage(new HttpMethod(ctx.Request.Method), "/" + rest + ctx.Request.QueryString);
    if (ctx.Request.ContentLength is > 0) {
        using var ms = new MemoryStream();
        await ctx.Request.Body.CopyToAsync(ms);
        req.Content = new ByteArrayContent(ms.ToArray());
        if (ctx.Request.ContentType is { } ct) {
            req.Content.Headers.TryAddWithoutValidation("Content-Type", ct);
        }
    }
    using var upstream = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ctx.RequestAborted);
    ctx.Response.StatusCode = (int)upstream.StatusCode;
    foreach (var h in upstream.Content.Headers) {
        ctx.Response.Headers[h.Key] = h.Value.ToArray();
    }
    await upstream.Content.CopyToAsync(ctx.Response.Body, ctx.RequestAborted);
});

// Sync server endpoints (/api/v1/*). Explicit routes win over the component fallback.
if (hasDb) {
    var build = new VerifyInfo { Name = "EggLedger", Sha256 = "dev", Version = "dev", Date = "dev" };

    if (!string.IsNullOrEmpty(cfg.DiscordClientId))
        DiscordOAuth.Init(cfg.DiscordClientId, cfg.DiscordClientSecret, cfg.RedirectUrl);

    Console.WriteLine($"eggledger: DB configured, running migrations. selfBase={selfBase}");
    var conn = await Database.InitAsync(cfg.DatabaseUrl);
    await Migrator.MigrateAsync(conn, Path.Combine(AppContext.BaseDirectory, "Migrations"));
    Console.WriteLine("eggledger: migrations complete, /api/v1 + Postgres storage active");

    if (!string.IsNullOrEmpty(cfg.BotToken)) {
        try {
            await SyncKitBot.StartAsync(new BotConfig {
                Name = "EggLedger",
                Token = cfg.BotToken,
                AppId = cfg.DiscordClientId,
                GuildId = cfg.GuildId,
                RepoUrl = "https://github.com/DavidArthurCole/EggLedger",
                Build = build,
                DeployAgentUrl = cfg.DeployAgentUrl,
                DeployAgentSecret = cfg.DeployAgentSecret,
                SharedRoleId = cfg.SharedRoleId,
            });
        } catch (Exception ex) {
            Console.Error.WriteLine($"synckit: bot start failed, continuing: {ex.Message}");
        }
    }

    Api.Map(app, cfg, build);
} else {
    Console.WriteLine("eggledger: WARNING - DATABASE_URL not set. /api/v1 + Postgres storage DISABLED; auth/sync will not work. UI boots only.");
}

app.MapRazorComponents<AppHost>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(App).Assembly);

app.Run();
