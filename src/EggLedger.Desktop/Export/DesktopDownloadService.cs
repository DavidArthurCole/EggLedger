using EggLedger.Domain.Export;
using EggLedger.Web.Platform;
using EggLedger.Web.Services;

namespace EggLedger.Desktop.Export;

/// <summary>
/// Desktop export sink. Produces the Domain export bytes, asks the native save
/// dialog for a path, writes the bytes, and reveals the file. A cancelled dialog
/// (null path) is a no-op.
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
