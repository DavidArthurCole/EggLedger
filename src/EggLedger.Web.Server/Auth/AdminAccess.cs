using System.Security.Claims;
using EggLedger.Web.Components.Admin;
using SyncKit.Auth;
using SyncKit.Contract;

namespace EggLedger.Web.Server.Auth;

public sealed class AdminAccess : IAdminAccess {
    public bool IsAdmin(ClaimsPrincipal user) =>
        user.IsAtLeast(UserRole.Admin) || user.FindFirst(AuthScheme.RoleClaim)?.Value == "admin";

    public Guid? CurrentUserId(ClaimsPrincipal user) =>
        user.SyncKitUserId() ?? (Guid.TryParse(user.FindFirst(AuthScheme.UserIdClaim)?.Value, out var id) ? id : null);
}
