using System.Security.Claims;
using EggIdentity.Auth;
using EggIdentity.Contract;
using EggLedger.Web.Components.Admin;

namespace EggLedger.Web.Server.Auth;

public sealed class AdminAccess : IAdminAccess {
    public bool IsAdmin(ClaimsPrincipal user) =>
        user.IsAtLeast(UserRole.Admin) || user.FindFirst(AuthScheme.RoleClaim)?.Value == "admin";

    public Guid? CurrentUserId(ClaimsPrincipal user) =>
        user.EggIdentityUserId() ?? (Guid.TryParse(user.FindFirst(AuthScheme.UserIdClaim)?.Value, out var id) ? id : null);
}
