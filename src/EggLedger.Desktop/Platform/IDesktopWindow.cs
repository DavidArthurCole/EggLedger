namespace EggLedger.Desktop.Platform;

/// <summary>
/// Seam over the host window features used by <see cref="DesktopPlatformCapabilities"/>:
/// reading the current size, showing the native save dialog, and exiting the
/// process. The real impl wraps the Photino window (native dialogs cannot run
/// headless, so they are isolated behind this interface and manual-verified);
/// tests inject a fake.
/// </summary>
public interface IDesktopWindow
{
    /// <summary>Current window size in pixels.</summary>
    (int Width, int Height) GetSize();

    /// <summary>
    /// Show the native save-file dialog seeded with <paramref name="defaultName"/>.
    /// Returns the chosen absolute path, or null when the user cancels.
    /// </summary>
    string? ShowSaveFileDialog(string defaultName);

    /// <summary>Terminate the current process (used after relaunch on restart).</summary>
    void ExitProcess();
}
