using System.Security.Claims;
using EggLedger.Web.Components.Admin;

namespace EggLedger.Web.Server.Auth;

public sealed class AdminAccess : IAdminAccess {
    public bool IsAdmin(ClaimsPrincipal user) =>
        user.FindFirst(AuthScheme.RoleClaim)?.Value == "admin";
}
