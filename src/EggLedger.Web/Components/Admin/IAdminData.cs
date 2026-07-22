using EggLedger.Web.Services;

namespace EggLedger.Web.Components.Admin;

public interface IAdminData {
    Task<IReadOnlyList<AdminUser>> GetUsersAsync(CancellationToken ct = default);
    Task<bool> DeleteUserAsync(Guid userId, CancellationToken ct = default);
}
