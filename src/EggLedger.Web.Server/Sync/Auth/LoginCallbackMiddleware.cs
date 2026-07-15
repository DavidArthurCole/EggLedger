using EggLedger.Web.Server.Sync.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;

namespace EggLedger.Web.Server.Sync.Auth;

// Catches SyncKit's redirect-mode login callback (?code=... or ?error=login_failed) on any GET
// request, since 0.6 mode=redirect returns the browser to the exact page login started on, not
// a fixed /auth/callback route. Redeems the code, signs in, then 302s to the same URL with the
// auth query params stripped so a page refresh never re-submits a spent code.
public sealed class LoginCallbackMiddleware(RequestDelegate next, ILogger<LoginCallbackMiddleware> logger) {
    public async Task InvokeAsync(HttpContext ctx, AuthEndpoints auth) {
        if (!HttpMethods.IsGet(ctx.Request.Method)) {
            await next(ctx);
            return;
        }

        var query = ctx.Request.Query;
        var hasCode = query.TryGetValue("code", out var codeValue) && !string.IsNullOrEmpty(codeValue);
        var hasError = query.ContainsKey("error");
        if (!hasCode && !hasError) {
            await next(ctx);
            return;
        }

        if (hasCode) {
            try {
                await auth.RedeemAndSignInAsync(ctx, codeValue.ToString(), ctx.RequestAborted);
            } catch (HttpRequestException ex) {
                logger.LogWarning(ex, "login callback: redeem failed");
            }
        }

        ctx.Response.Redirect(BuildRedirectTarget(ctx.Request.Path, query));
    }

    internal static string BuildRedirectTarget(PathString path, IQueryCollection query) {
        var clean = new QueryBuilder();
        foreach (var (key, value) in query) {
            if (key is "code" or "error") {
                continue;
            }
            foreach (var v in value) {
                if (v is not null) {
                    clean.Add(key, v);
                }
            }
        }
        return path + clean.ToQueryString();
    }
}
