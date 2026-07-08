using System.Security.Claims;
using EggLedger.Web.Server.Sync;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Npgsql;
using SyncKit.Identity.Client;

namespace EggLedger.Web.Server.Auth;

// Additional OIDC challenge scheme against the self-hosted Authentik instance, dual-run
// alongside the existing direct Discord OAuth (AuthEndpoints/DiscordOAuth). Wired only when
// AUTHENTIK_AUTHORITY is set, mirroring the existing conditional Discord wiring in Program.cs.
// OnTicketReceived resolves the OIDC login to a provider-neutral user_id via SyncKit.Identity
// and stamps it (plus role) onto the principal before sign-in completes.
public static class AuthentikAuth {
    public static bool AddIfConfigured(AuthenticationBuilder builder, AppConfig cfg, IdentityApiClient identity, NpgsqlDataSource source) {
        if (string.IsNullOrEmpty(cfg.AuthentikAuthority)) {
            return false;
        }

        builder.AddOpenIdConnect(o => {
            o.Authority = cfg.AuthentikAuthority;
            o.ClientId = cfg.AuthentikClientId;
            o.ClientSecret = cfg.AuthentikClientSecret;
            o.ResponseType = OpenIdConnectResponseType.Code;
            o.SignInScheme = AuthScheme.Cookie;
            o.Scope.Clear();
            o.Scope.Add("openid");
            o.Scope.Add("profile");
            o.Scope.Add("email");
            o.Scope.Add("discord_id");
            o.SaveTokens = true;
            o.GetClaimsFromUserInfoEndpoint = true;
            o.CallbackPath = "/auth";
            o.Events.OnTicketReceived = async ctx => {
                var principal = ctx.Principal!;
                var sub = principal.FindFirstValue("sub");
                if (string.IsNullOrEmpty(sub)) {
                    ctx.Response.Redirect("/?login=failed");
                    ctx.HandleResponse();
                    return;
                }
                var discordId = principal.FindFirstValue("discord_id");
                var username = principal.FindFirstValue("preferred_username") ?? principal.FindFirstValue("name") ?? "";
                var result = await identity.ResolveAsync(
                    "authentik", sub, discordId, username, avatar: null, ctx.HttpContext.RequestAborted);

                // SyncKit.Identity only manages the (provider, subject) -> user_id mapping in
                // its own store; the local users row (which EnsureEncryptionKeyAsync updates)
                // must be created here since only the Discord OAuth path did so before.
                await using (var cmd = source.CreateCommand(
                    "INSERT INTO users (user_id, created_at) VALUES ($1, $2) ON CONFLICT (user_id) DO NOTHING")) {
                    cmd.Parameters.AddWithValue(result.UserId);
                    cmd.Parameters.AddWithValue(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                    await cmd.ExecuteNonQueryAsync(ctx.HttpContext.RequestAborted);
                }

                var claimsIdentity = (ClaimsIdentity)principal.Identity!;
                claimsIdentity.AddClaim(new Claim(AuthScheme.UserIdClaim, result.UserId.ToString()));
                claimsIdentity.AddClaim(new Claim(AuthScheme.RoleClaim, result.Role));
                if (!string.IsNullOrEmpty(discordId)) {
                    claimsIdentity.AddClaim(new Claim(AuthScheme.DiscordIdClaim, discordId));
                }
            };
            o.Events.OnRemoteFailure = ctx => {
                ctx.Response.Redirect("/?login=failed");
                ctx.HandleResponse();
                return Task.CompletedTask;
            };
        });
        return true;
    }
}
