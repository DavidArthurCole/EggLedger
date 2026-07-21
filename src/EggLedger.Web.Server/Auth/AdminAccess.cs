using System.Security.Claims;
using EggLedger.Web.Components.Admin;

namespace EggLedger.Web.Server.Auth;

public sealed class AdminAccess : IAdminAccess {
    public bool IsAdmin(ClaimsPrincipal user) =>
        user.FindFirst(AuthScheme.RoleClaim)?.Value == "admin";

    public Guid? CurrentUserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst(AuthScheme.UserIdClaim)?.Value, out var id) ? id : null;
}
