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
    string MennoFunctionKey,
    IReadOnlySet<string> AdminDiscordIds) {
    public const string MennoUpstreamUrl = "https://eggincdatacollection.azurewebsites.net/api/SubmitEid";

    public static AppConfig FromEnv(Func<string, string?> get) {
        string V(string k) => get(k) ?? string.Empty;
        var addr = V("LISTEN_ADDR");
        var admins = V("ADMIN_DISCORD_IDS")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);
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
            MennoFunctionKey: V("MENNO_FUNCTION_KEY"),
            AdminDiscordIds: admins);
    }
}
