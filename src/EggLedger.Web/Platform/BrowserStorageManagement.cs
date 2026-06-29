using EggLedger.Domain.Export;

namespace EggLedger.Web.Platform;

/// <summary>Browser no-ops for the desktop storage seams. The Storage/Export panels are gated on IsDesktop, so these never run.</summary>
public sealed class BrowserStorageManagement : IStorageManagement, IExportManagement {
    public string GetDataRootDir() => "";
    public Task BackupAsync(string destPath, bool db, bool exports, bool logs) => Task.CompletedTask;
    public Task MoveAsync(string destPath) => Task.CompletedTask;
    public Task<List<ExportGroup>> ListAsync() => Task.FromResult(new List<ExportGroup>());
    public Task<(int deleted, long freedBytes)> PruneAsync(int keepCount) => Task.FromResult((0, 0L));
    public Task DeleteAsync(IEnumerable<string> paths) => Task.CompletedTask;
}
