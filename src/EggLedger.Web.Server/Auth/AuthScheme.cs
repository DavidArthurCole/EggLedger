namespace EggLedger.Web.Server.Auth;

public static class AuthScheme {
    public const string Cookie = "el_cookie";

    /// <summary>Claim type carrying the Discord user id in the auth cookie.</summary>
    public const string DiscordIdClaim = "discord_id";

    /// <summary>Claim type carrying the provider-neutral user id (UUID) in the auth cookie.</summary>
    public const string UserIdClaim = "user_id";

    /// <summary>Claim type carrying the SyncKit.Identity role (viewer/contributor/admin) in the auth cookie.</summary>
    public const string RoleClaim = "role";
}
