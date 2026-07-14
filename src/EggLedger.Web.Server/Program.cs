using EggLedger.Web;
using EggLedger.Web.Data;
using EggLedger.Web.Server.Components;
using EggLedger.Web.Server.Sync;
using EggLedger.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
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

// Registered so components (e.g. ConnectGate) can check cfg.AuthentikAuthority to decide
// whether to show the Authentik login option, without threading it through as a parameter.
builder.Services.AddSingleton(cfg);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(o => o.DetailedErrors = builder.Environment.IsDevelopment());

// Behind nginx TLS termination: trust the forwarded proto/host so the app knows it is
// HTTPS (secure cookies + correct OAuth redirect scheme). Only forwarded headers from the
// configured proxy networks are trusted, so a client off the trusted path can't spoof its IP/host.
builder.Services.Configure<ForwardedHeadersOptions>(o => {
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
    o.ForwardLimit = null;
    foreach (var cidr in cfg.TrustedProxyNetworks) {
        if (System.Net.IPNetwork.TryParse(cidr, out var net)) {
            o.KnownIPNetworks.Add(net);
        }
    }
});

// Cookie auth carries the Discord identity into the Blazor circuit via the framework
// AuthenticationStateProvider (the OAuth callback signs in; the circuit reads claims).
var authBuilder = builder.Services.AddAuthentication(EggLedger.Web.Server.Auth.AuthScheme.Cookie)
    .AddCookie(EggLedger.Web.Server.Auth.AuthScheme.Cookie, o => {
        o.Cookie.Name = "el_session";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        o.ExpireTimeSpan = TimeSpan.FromDays(30);
        o.SlidingExpiration = true;
        // A cookie can decrypt fine but carry a stale/unparseable user_id claim (e.g. after
        // a user_id migration or DB reset) - reject it here so the request is treated as
        // unauthenticated before a Blazor circuit ever starts, instead of crashing the
        // circuit deep inside a write path (CurrentUser.RequireUserIdAsync).
        o.Events.OnValidatePrincipal = async ctx => {
            var claim = ctx.Principal?.FindFirst(EggLedger.Web.Server.Auth.AuthScheme.UserIdClaim)?.Value;
            if (!Guid.TryParse(claim, out _)) {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync(EggLedger.Web.Server.Auth.AuthScheme.Cookie);
                return;
            }
            var identity = ctx.HttpContext.RequestServices.GetService<SyncKit.Identity.Client.IdentityApiClient>();
            if (identity is not null) {
                await SyncKit.Auth.AuthentikAspNetAuth.OnValidatePrincipalCheckRevoked(ctx, identity);
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

if (hasDb) {
    var dataSource = NpgsqlDataSource.Create(cfg.DatabaseUrl);
    builder.Services.AddSingleton(dataSource);

    // Persist the DataProtection key ring to Postgres so cookie-auth keys survive
    // container redeploys (otherwise every deploy invalidates all sessions).
    var dp = builder.Services.AddDataProtection()
        .SetApplicationName("EggLedger")
        .AddKeyManagementOptions(o =>
            o.XmlRepository = new EggLedger.Web.Server.Auth.PostgresXmlRepository(dataSource));

    // Cert-encrypt the keyring at rest so a raw Postgres dump doesn't leak usable keys.
    // Prod cert provisioning happens separately, so a missing/unreadable cert only warns,
    // never crashes boot - losing at-rest encryption is recoverable, a crash loop isn't.
    if (!string.IsNullOrEmpty(cfg.DataProtectionCertPath)) {
        try {
            var cert = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12FromFile(
                cfg.DataProtectionCertPath, cfg.DataProtectionCertPassword);
            dp.ProtectKeysWithCertificate(cert);
        } catch (Exception ex) {
            Console.Error.WriteLine($"eggledger: WARNING - failed to load DataProtection cert from {cfg.DataProtectionCertPath}: {ex.Message}. DataProtection keyring is stored unencrypted in Postgres.");
        }
    } else {
        Console.Error.WriteLine("eggledger: WARNING - DATA_PROTECTION_CERT_PATH not set. DataProtection keyring is stored unencrypted in Postgres.");
    }

    // Both Discord and Authentik logins resolve their user_id through SyncKit.Identity now;
    // there's no meaningful login without a database to store the local app-data row in
    // anyway (mirrors DiscordOAuth.Init below, also gated on hasDb). Constructed inline (not
    // via AddHttpClient<T>) since AuthentikAuth.AddIfConfigured needs it now, before app.Build().
    var identityHttp = new HttpClient { BaseAddress = new Uri(cfg.IdentityApiUrl) };
    identityHttp.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", cfg.IdentityApiSecret);
    var identityClient = new SyncKit.Identity.Client.IdentityApiClient(identityHttp);
    builder.Services.AddSingleton(identityClient);
    builder.Services.AddSingleton<EggLedger.Web.Server.Sync.Auth.ICurrentUser, EggLedger.Web.Server.Sync.Auth.CurrentUser>();

    EggLedger.Web.Server.Auth.AuthentikAuth.AddIfConfigured(authBuilder, cfg, identityClient, dataSource);
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

// Server-side per-user storage, only when a DB is configured (dev without DB keeps the
// browser IndexedDB path). PostgresIndexedDb scopes every op to the circuit's CurrentUser.
builder.Services.AddScoped<EggLedger.Web.Server.Storage.CurrentUser>();
if (hasDb) {
    builder.Services.AddScoped<ISessionStore>(sp =>
        new EggLedger.Web.Server.Sync.Db.SessionStore(
            sp.GetRequiredService<NpgsqlDataSource>(),
            sp.GetRequiredService<SyncKit.Identity.Client.IdentityApiClient>()));
    builder.Services.RemoveAll<IIndexedDb>();
    builder.Services.AddScoped<IIndexedDb>(sp => new EggLedger.Web.Server.Storage.PostgresIndexedDb(
        sp.GetRequiredService<NpgsqlDataSource>(),
        sp.GetRequiredService<EggLedger.Web.Server.Storage.CurrentUser>()));
    builder.Services.AddScoped<EggLedger.Web.Server.Storage.FirstLoginBackfill>();
}

// Ship 3D assets read from a server-local dir (content root /ships), served only via the
// auth-gated endpoint. Missing dir -> empty manifest, endpoint 404s gracefully.
builder.Services.AddSingleton(_ =>
    new EggLedger.Web.Server.Ships.ShipAssetService(
        Path.Combine(builder.Environment.ContentRootPath, "ships")));

// Outbound client for the egg-api forward (server calls auxbrain directly; no browser CORS).
builder.Services.AddHttpClient("auxbrain", c => c.BaseAddress = new Uri("https://www.auxbrain.com"));

var app = builder.Build();

app.UseForwardedHeaders();
// Single routing pass for the whole pipeline. Must precede auth/antiforgery/endpoints so the
// Blazor endpoint has antiforgery ahead of it; Api.Map's metrics middleware reads GetEndpoint().
app.UseRouting();
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
    // Discord OAuth backs /api/v1/auth/*, which Api.Map always wires up when hasDb. Fail
    // startup here instead of letting every auth request 500 confusingly at request time.
    if (string.IsNullOrEmpty(cfg.DiscordClientId) || string.IsNullOrEmpty(cfg.DiscordClientSecret)) {
        app.Logger.LogCritical("eggledger: DATABASE_URL is set but DISCORD_CLIENT_ID/DISCORD_CLIENT_SECRET are missing. Auth routes cannot function; refusing to start.");
        throw new InvalidOperationException("Discord OAuth is not configured but a database is; set DISCORD_CLIENT_ID/DISCORD_CLIENT_SECRET or unset DATABASE_URL.");
    }
    DiscordOAuth.Init(cfg.DiscordClientId, cfg.DiscordClientSecret, cfg.RedirectUrl);

    var build = new VerifyInfo { Name = "EggLedger", Sha256 = cfg.BuildSha, Version = EggLedger.Web.AppVersionInfo.Current, Date = cfg.BuildDate };

    app.Logger.LogInformation("eggledger: DB configured, running migrations. selfBase={SelfBase}", selfBase);
    var conn = await Database.InitAsync(cfg.DatabaseUrl);
    await Migrator.MigrateAsync(conn, Path.Combine(AppContext.BaseDirectory, "Migrations"));
    app.Logger.LogInformation("eggledger: migrations complete, /api/v1 + Postgres storage active");

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
            app.Logger.LogWarning(ex, "synckit: bot start failed, continuing");
        }
    }

    Api.Map(app, cfg, build);
} else {
    app.Logger.LogWarning("eggledger: WARNING - DATABASE_URL not set. /api/v1 + Postgres storage DISABLED; auth/sync will not work. UI boots only.");
}

app.MapRazorComponents<AppHost>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(App).Assembly);

app.Run();
