using System.Security.Claims;

namespace EggLedger.Web.Components.Admin;

public interface IAdminAccess {
    bool IsAdmin(ClaimsPrincipal user);
    Guid? CurrentUserId(ClaimsPrincipal user);
}
