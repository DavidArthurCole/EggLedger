using EggLedger.Domain.Export;
using EggLedger.Web.Platform;
using EggLedger.Web.Services;

namespace EggLedger.Desktop.Export;

/// <summary>
/// Desktop export sink. Instead of a browser download, it produces the same Domain
/// export bytes (<see cref="MissionExport"/>), asks the native save dialog for a
/// path (<see cref="IPlatformCapabilities.ChooseSaveFilePathAsync"/>), writes the
/// bytes there, and reveals the file in the OS file browser. A cancelled dialog
/// (null path) is a no-op. This is the .NET port of the Go desktop behavior where
/// exports are written to disk and revealed, not streamed to a browser.
/// </summary>
public sealed class DesktopDownloadService(IPlatformCapabilities platform) : IDownloadService
{
    private readonly IPlatformCapabilities _platform = platform;

    public ValueTask DownloadCsvAsync(IReadOnlyList<Mission> missions, string filename)
        => SaveAsync(MissionExport.MissionsToCsvBytes(missions), filename);

    public ValueTask DownloadXlsxAsync(IReadOnlyList<Mission> missions, string filename)
        => SaveAsync(MissionExport.MissionsToXlsxBytes(missions), filename);

    private async ValueTask SaveAsync(byte[] bytes, string filename)
    {
        var path = await _platform.ChooseSaveFilePathAsync(filename).ConfigureAwait(false);
        if (string.IsNullOrEmpty(path))
        {
            // User cancelled the native save dialog: write nothing, reveal nothing.
            return;
        }
        await File.WriteAllBytesAsync(path, bytes).ConfigureAwait(false);
        await _platform.OpenFileInFolderAsync(path).ConfigureAwait(false);
    }
}
