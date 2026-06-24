using EggLedger.Domain.Export;

namespace EggLedger.Web.Services;

/// <summary>Host-agnostic export sink for mission CSV/XLSX bytes. Browser streams to a download; desktop saves via the native dialog.</summary>
public interface IDownloadService {
    ValueTask DownloadCsvAsync(IReadOnlyList<Mission> missions, string filename);

    ValueTask DownloadXlsxAsync(IReadOnlyList<Mission> missions, string filename);
}
