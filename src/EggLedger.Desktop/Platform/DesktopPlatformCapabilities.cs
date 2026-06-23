using System.Runtime.InteropServices;
using EggLedger.Web.Platform;

namespace EggLedger.Desktop.Platform;

/// <summary>
/// Native desktop implementation of <see cref="IPlatformCapabilities"/> for the
/// Photino host. Open + reveal-in-folder shell out via <see cref="IProcessRunner"/>
/// using the per-OS commands from <see cref="DesktopCommandBuilder"/> (ported from
/// the Go platform package). The save dialog and window size come from the Photino
/// window via <see cref="IDesktopWindow"/>. Restart relaunches the current exe then
/// exits. <see cref="IsDesktop"/> is true so desktop-only UI is enabled.
/// </summary>
public sealed class DesktopPlatformCapabilities(IProcessRunner processRunner, IDesktopWindow window) : IPlatformCapabilities
{
    private readonly IProcessRunner _processRunner = processRunner;
    private readonly IDesktopWindow _window = window;

    public bool IsDesktop => true;

    public Task OpenFileAsync(string path)
    {
        // Absolutize before shelling out, matching Go platform's filepath.Abs; the
        // command builder stays pure so absolutization lives here in the host layer.
        var (exe, args) = DesktopCommandBuilder.BuildOpenCommand(CurrentPlatform(), Path.GetFullPath(path));
        return _processRunner.RunAsync(exe, args);
    }

    public Task OpenFileInFolderAsync(string path)
    {
        var (exe, args) = DesktopCommandBuilder.BuildOpenInFolderCommand(CurrentPlatform(), Path.GetFullPath(path));
        return _processRunner.RunAsync(exe, args);
    }

    /// <summary>
    /// MANUAL-VERIFY: delegates to the native Photino save dialog. Returns the
    /// chosen path or null on cancel (the isolated, testable contract; the dialog
    /// itself is not unit-tested).
    /// </summary>
    public Task<string?> ChooseSaveFilePathAsync(string defaultName)
        => Task.FromResult(_window.ShowSaveFileDialog(defaultName));

    /// <summary>
    /// Relaunch the current executable then exit. The relaunch command is built from
    /// the running process path (testable via the runner seam); the actual exit is
    /// MANUAL-VERIFY. This is only the restart primitive; the self-update download
    /// and handshake are D4.
    /// </summary>
    public async Task RestartAppAsync()
    {
        var exePath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exePath))
        {
            var (exe, args) = DesktopCommandBuilder.BuildRestartCommand(exePath);
            await _processRunner.RunAsync(exe, args);
        }
        _window.ExitProcess();
    }

    public Task<(int w, int h)> GetWindowSizeAsync()
    {
        var (width, height) = _window.GetSize();
        return Task.FromResult((width, height));
    }

    private static OSPlatform CurrentPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return OSPlatform.Windows;
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return OSPlatform.OSX;
        }
        return OSPlatform.Linux;
    }
}
