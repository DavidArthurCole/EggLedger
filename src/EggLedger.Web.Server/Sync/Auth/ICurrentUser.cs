using Microsoft.AspNetCore.Http;
using SyncKit.Identity.Client;

namespace EggLedger.Web.Server.Sync.Auth;

// Distinct from Storage.CurrentUser (the Blazor-circuit, AuthenticationStateProvider-based
// one). This is for the minimal-API /api/v1/* and /api/ships/* surface, which RequireAuth gates
// via a bearer token -> X-Discord-ID header (actually a user_id GUID string post-migration) and
// never populates HttpContext.User/ClaimsPrincipal at all. Role isn't in that header, so it's
// resolved with one identity-API call per request and cached on HttpContext.Items for the
// lifetime of the request.
public interface ICurrentUser {
    Guid? UserId(HttpContext ctx);
    Task<string?> RoleAsync(HttpContext ctx, CancellationToken ct);
    Task<bool> IsAtLeastAsync(HttpContext ctx, UserRole role, CancellationToken ct);
}

public enum UserRole { Viewer = 0, Contributor = 1, Admin = 2 }

public sealed class CurrentUser(IdentityApiClient identity) : ICurrentUser {
    private const string RoleItemsKey = "SyncKit.Identity.Role";

    public Guid? UserId(HttpContext ctx) =>
        Guid.TryParse(ctx.Request.Headers["X-Discord-ID"].ToString(), out var id) ? id : null;

    public async Task<string?> RoleAsync(HttpContext ctx, CancellationToken ct) {
        if (ctx.Items.TryGetValue(RoleItemsKey, out var cached)) return (string?)cached;
        var userId = UserId(ctx);
        if (userId is null) { ctx.Items[RoleItemsKey] = null; return null; }
        var user = await identity.GetAsync(userId.Value, ct);
        var role = user?.Role;
        ctx.Items[RoleItemsKey] = role;
        return role;
    }

    public async Task<bool> IsAtLeastAsync(HttpContext ctx, UserRole role, CancellationToken ct) {
        var rawRole = await RoleAsync(ctx, ct);
        if (rawRole is null) return false;
        return ParseRole(rawRole) >= role;
    }

    private static UserRole ParseRole(string? role) => role?.ToLowerInvariant() switch {
        "admin" => UserRole.Admin,
        "contributor" => UserRole.Contributor,
        _ => UserRole.Viewer,
    };
}
