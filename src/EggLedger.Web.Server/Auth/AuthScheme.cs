namespace EggLedger.Web.Server.Auth;

public static class AuthScheme {
    public const string Cookie = "el_cookie";

    /// <summary>Claim type carrying the Discord user id in the auth cookie.</summary>
    public const string DiscordIdClaim = "discord_id";
}
