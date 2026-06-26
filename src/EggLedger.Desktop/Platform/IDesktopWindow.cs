namespace EggLedger.Desktop.Platform;

/// <summary>Seam over host window features (size, native save dialog, process exit).</summary>
/// <remarks>Native dialogs are isolated here because they cannot run headless; tests inject a fake.</remarks>
public interface IDesktopWindow {
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
