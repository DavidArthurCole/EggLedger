using EggLedger.Web.Server.Sync;
using Microsoft.AspNetCore.Authentication;
using Npgsql;
using SyncKit.Auth;
using SyncKit.Identity.Client;

namespace EggLedger.Web.Server.Auth;

// Additional OIDC challenge scheme against the self-hosted Authentik instance, dual-run
// alongside the existing direct Discord OAuth (AuthEndpoints/DiscordOAuth). Wired only when
// AUTHENTIK_AUTHORITY is set, mirroring the existing conditional Discord wiring in Program.cs.
// Delegates the OIDC handler shape to SyncKit.Auth.AuthentikAspNetAuth, shared with EggIncognito.
public static class AuthentikAuth {
    // identity param kept for signature/call-site compatibility (Program.cs:112, existing tests);
    // the shared helper resolves IdentityApiClient itself from DI per-request instead of using it.
    public static bool AddIfConfigured(AuthenticationBuilder builder, AppConfig cfg, IdentityApiClient _, NpgsqlDataSource source) {
        if (string.IsNullOrEmpty(cfg.AuthentikAuthority)) {
            return false;
        }

        return AuthentikAspNetAuth.AddIfConfigured(builder, new AuthentikAspNetAuthOptions {
            CookieScheme = AuthScheme.Cookie,
            Authority = cfg.AuthentikAuthority,
            ClientId = cfg.AuthentikClientId,
            ClientSecret = cfg.AuthentikClientSecret,
            CallbackPath = "/auth",
            UserIdClaim = AuthScheme.UserIdClaim,
            RoleClaim = AuthScheme.RoleClaim,
            DiscordIdClaim = AuthScheme.DiscordIdClaim,
            OnResolved = async (result, _, ctx) => {
                // SyncKit.Identity only manages the (provider, subject) -> user_id mapping in
                // its own store; the local users row (which EnsureEncryptionKeyAsync updates)
                // must be created here since only the Discord OAuth path did so before.
                await using var cmd = source.CreateCommand(
                    "INSERT INTO users (user_id, created_at) VALUES ($1, $2) ON CONFLICT (user_id) DO NOTHING");
                cmd.Parameters.AddWithValue(result.UserId);
                cmd.Parameters.AddWithValue(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                await cmd.ExecuteNonQueryAsync(ctx.RequestAborted);
            },
        });
    }
}
