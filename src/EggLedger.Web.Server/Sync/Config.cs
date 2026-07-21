namespace EggLedger.Web.Server.Sync;

public sealed record AppConfig(
    string ListenAddr,
    string DatabaseUrl,
    string DiscordClientId,
    string DiscordClientSecret,
    string RedirectUrl,
    string BotToken,
    string GuildId,
    string SharedRoleId,
    string DeployAgentUrl,
    string DeployAgentSecret,
    string DashboardChannelId,
    string DeployNotifySecret,
    string MennoFunctionKey,
    string AuthentikAuthority,
    string AuthentikClientId,
    string AuthentikClientSecret,
    string IdentityApiUrl,
    string IdentityApiSecret,
    string IdentityWidgetUrl,
    IReadOnlyList<string> TrustedProxyNetworks,
    string BuildSha,
    string BuildDate,
    string DataProtectionCertPath,
    string DataProtectionCertPassword) {
    public const string MennoUpstreamUrl = "https://eggincdatacollection.azurewebsites.net/api/SubmitEid";

    // Private + ULA + loopback ranges. The container sits behind nginx on a private docker network,
    // so only forwarded headers from those sources are trusted. Override with TRUSTED_PROXY_NETWORKS.
    private static readonly string[] DefaultProxyNetworks =
        ["127.0.0.0/8", "::1/128", "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "fc00::/7", "fe80::/10"];

    public static AppConfig FromEnv(Func<string, string?> get) {
        string V(string k) => get(k) ?? string.Empty;
        var addr = V("LISTEN_ADDR");
        var proxyNets = V("TRUSTED_PROXY_NETWORKS")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new AppConfig(
            ListenAddr: string.IsNullOrEmpty(addr) ? ":8080" : addr,
            DatabaseUrl: V("DATABASE_URL"),
            DiscordClientId: V("DISCORD_CLIENT_ID"),
            DiscordClientSecret: V("DISCORD_CLIENT_SECRET"),
            RedirectUrl: get("OAUTH_REDIRECT_URL") is { Length: > 0 } r
                ? r
                : "https://eggledger.davidarthurcole.me/api/v1/auth/callback",
            BotToken: V("DISCORD_BOT_TOKEN"),
            GuildId: V("DISCORD_GUILD_ID"),
            SharedRoleId: V("SHARED_ROLE_ID"),
            DeployAgentUrl: V("DEPLOY_AGENT_URL"),
            DeployAgentSecret: V("DEPLOY_AGENT_SECRET"),
            DashboardChannelId: V("DISCORD_DASHBOARD_CHANNEL_ID"),
            DeployNotifySecret: V("DEPLOY_NOTIFY_SECRET"),
            MennoFunctionKey: V("MENNO_FUNCTION_KEY"),
            AuthentikAuthority: V("AUTHENTIK_AUTHORITY"),
            AuthentikClientId: V("AUTHENTIK_CLIENT_ID"),
            AuthentikClientSecret: V("AUTHENTIK_CLIENT_SECRET"),
            IdentityApiUrl: V("IDENTITY_API_URL"),
            IdentityApiSecret: V("IDENTITY_API_SECRET"),
            // Browser-facing address serving /synckit-login.js + the popup flow. Distinct from
            // IdentityApiUrl (server-to-server, e.g. an internal/loopback address under
            // network_mode: host) which a <script src> can't reach. Defaults to IdentityApiUrl
            // for deploys where both happen to be the same public host.
            IdentityWidgetUrl: get("IDENTITY_WIDGET_URL") is { Length: > 0 } widgetUrl ? widgetUrl : V("IDENTITY_API_URL"),
            TrustedProxyNetworks: proxyNets.Length > 0 ? proxyNets : DefaultProxyNetworks,
            BuildSha: get("BUILD_SHA") is { Length: > 0 } sha ? sha : "dev",
            BuildDate: get("BUILD_DATE") is { Length: > 0 } date ? date : "dev",
            DataProtectionCertPath: V("DATA_PROTECTION_CERT_PATH"),
            DataProtectionCertPassword: V("DATA_PROTECTION_CERT_PASSWORD"));
    }
}
