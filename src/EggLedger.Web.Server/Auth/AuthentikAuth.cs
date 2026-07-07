using System.Security.Claims;
using EggLedger.Web.Server.Sync;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace EggLedger.Web.Server.Auth;

// Additional OIDC challenge scheme against the self-hosted Authentik instance, dual-run
// alongside the existing direct Discord OAuth (AuthEndpoints/DiscordOAuth). Wired only when
// AUTHENTIK_AUTHORITY is set, mirroring the existing conditional Discord wiring in Program.cs.
// OnTicketReceived resolves the OIDC login to a provider-neutral user_id (see
// AuthentikIdentityResolver) and stamps it onto the principal before sign-in completes.
public static class AuthentikAuth {
    public static bool AddIfConfigured(AuthenticationBuilder builder, AppConfig cfg, AuthentikIdentityResolver resolver) {
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
                var userId = await resolver.ResolveAsync(sub, discordId, ctx.HttpContext.RequestAborted);

                var identity = (ClaimsIdentity)principal.Identity!;
                identity.AddClaim(new Claim(AuthScheme.UserIdClaim, userId.ToString()));
                if (!string.IsNullOrEmpty(discordId)) {
                    identity.AddClaim(new Claim(AuthScheme.DiscordIdClaim, discordId));
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
