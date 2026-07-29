namespace EggLedger.Web.Server.Sync;

public sealed record AppConfig(
    string ListenAddr,
    string DatabaseUrl,
    string DiscordClientId,
    string PublicBaseUrl,
    string BotToken,
    string GuildId,
    string SharedRoleId,
    string DeployAgentUrl,
    string DeployAgentSecret,
    string DashboardChannelId,
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
            PublicBaseUrl: get("PUBLIC_BASE_URL") is { Length: > 0 } pub ? pub : "https://eggledger.davidarthurcole.me",
            BotToken: V("DISCORD_BOT_TOKEN"),
            GuildId: V("DISCORD_GUILD_ID"),
            SharedRoleId: V("SHARED_ROLE_ID"),
            DeployAgentUrl: V("DEPLOY_AGENT_URL"),
            DeployAgentSecret: V("DEPLOY_AGENT_SECRET"),
            DashboardChannelId: V("DISCORD_DASHBOARD_CHANNEL_ID"),
            MennoFunctionKey: V("MENNO_FUNCTION_KEY"),
            AuthentikAuthority: V("AUTHENTIK_AUTHORITY"),
            AuthentikClientId: V("AUTHENTIK_CLIENT_ID"),
            AuthentikClientSecret: V("AUTHENTIK_CLIENT_SECRET"),
            IdentityApiUrl: V("IDENTITY_API_URL"),
            IdentityApiSecret: V("IDENTITY_API_SECRET"),




            IdentityWidgetUrl: get("IDENTITY_WIDGET_URL") is { Length: > 0 } widgetUrl ? widgetUrl : V("IDENTITY_API_URL"),
            TrustedProxyNetworks: proxyNets.Length > 0 ? proxyNets : DefaultProxyNetworks,
            BuildSha: get("BUILD_SHA") is { Length: > 0 } sha ? sha : "dev",
            BuildDate: get("BUILD_DATE") is { Length: > 0 } date ? date : "dev",
            DataProtectionCertPath: V("DATA_PROTECTION_CERT_PATH"),
            DataProtectionCertPassword: V("DATA_PROTECTION_CERT_PASSWORD"));
    }
}
