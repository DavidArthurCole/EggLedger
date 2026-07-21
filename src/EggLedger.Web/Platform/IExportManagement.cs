using EggLedger.Domain.Export;

namespace EggLedger.Web.Platform;

public interface IExportManagement {
    Task<List<ExportGroup>> ListAsync();

    Task<(int deleted, long freedBytes)> PruneAsync(int keepCount);

    Task DeleteAsync(IEnumerable<string> paths);
}
