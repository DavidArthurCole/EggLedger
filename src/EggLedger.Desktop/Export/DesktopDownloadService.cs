using System.Text;
using EggLedger.Domain.Export;
using EggLedger.Web.Platform;
using EggLedger.Web.Services;
using Microsoft.JSInterop;

namespace EggLedger.Desktop.Export;

/// <summary>
/// Desktop export sink. Produces the Domain export bytes, asks the native save
/// dialog for a path, writes the bytes, and reveals the file. A cancelled dialog
/// (null path) is a no-op. Report JSON import has no native open-file dialog, so
/// it reuses the browser file-picker JS module (the WebView has a real DOM too).
/// </summary>
public sealed class DesktopDownloadService(IPlatformCapabilities platform, IJSRuntime js) : IDownloadService {
    private const string ModulePath = "./_content/EggLedger.Web/js/download.js";
    private readonly IPlatformCapabilities _platform = platform;
    private readonly IJSRuntime _js = js;

    public ValueTask DownloadCsvAsync(IReadOnlyList<Mission> missions, string filename)
        => SaveAsync(MissionExport.MissionsToCsvBytes(missions), filename);

    public ValueTask DownloadXlsxAsync(IReadOnlyList<Mission> missions, string filename)
        => SaveAsync(MissionExport.MissionsToXlsxBytes(missions), filename);

    public ValueTask DownloadJsonAsync(string json, string filename)
        => SaveAsync(Encoding.UTF8.GetBytes(json), filename);

    public async ValueTask<string?> PickJsonFileAsync() {
        var module = await _js.InvokeAsync<IJSObjectReference>("import", ModulePath).ConfigureAwait(false);
        await using (module.ConfigureAwait(false)) {
            return await module.InvokeAsync<string?>("pickTextFile", ".json,application/json").ConfigureAwait(false);
        }
    }

    private async ValueTask SaveAsync(byte[] bytes, string filename) {
        var path = await _platform.ChooseSaveFilePathAsync(filename).ConfigureAwait(false);
        if (string.IsNullOrEmpty(path)) {
            // User cancelled the native save dialog: write nothing, reveal nothing.
            return;
        }
        await File.WriteAllBytesAsync(path, bytes).ConfigureAwait(false);
        await _platform.OpenFileInFolderAsync(path).ConfigureAwait(false);
    }
}
