using EggLedger.Domain.Export;

namespace EggLedger.Web.Services;

/// <summary>
/// Export sink for mission CSV/XLSX bytes. The browser implementation streams the
/// bytes to a browser download (the download.js shim); the desktop host saves them
/// to a user-chosen path via the native save dialog and reveals the file. The UI
/// binds this interface so it is host-agnostic (mirrors the D2 IReportRunner /
/// IMissionStore seam).
/// </summary>
public interface IDownloadService
{
    /// <summary>Exports the missions as a CSV named <paramref name="filename"/>.</summary>
    ValueTask DownloadCsvAsync(IReadOnlyList<Mission> missions, string filename);

    /// <summary>Exports the missions as an XLSX named <paramref name="filename"/>.</summary>
    ValueTask DownloadXlsxAsync(IReadOnlyList<Mission> missions, string filename);
}
