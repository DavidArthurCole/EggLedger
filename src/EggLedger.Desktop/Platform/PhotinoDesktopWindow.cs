using Photino.NET;

namespace EggLedger.Desktop.Platform;

/// <summary>
/// Real <see cref="IDesktopWindow"/> backed by the Photino window. Window size and
/// the native save dialog come straight from Photino's cross-platform API (no
/// P/Invoke, no osascript/zenity shell-outs needed). The save dialog and exit are
/// MANUAL-VERIFY: a native picker cannot be exercised headlessly.
///
/// The window is bound late via <see cref="Attach"/>: the Photino MainWindow only
/// exists after the host is built, but DI is wired before build. The holder is
/// registered up front and attached once the app has its window.
/// </summary>
public sealed class PhotinoDesktopWindow : IDesktopWindow
{
    private PhotinoWindow? _window;

    public PhotinoDesktopWindow()
    {
    }

    public PhotinoDesktopWindow(PhotinoWindow window) => _window = window;

    /// <summary>Bind the Photino window once the host has built it.</summary>
    public void Attach(PhotinoWindow window) => _window = window;

    public (int Width, int Height) GetSize()
    {
        var w = Window;
        return (w.Width, w.Height);
    }

    /// <summary>
    /// MANUAL-VERIFY: shows Photino's native save dialog filtered to JSON (mirrors
    /// the Go ChooseSaveFilePath filter). Photino returns null/empty on cancel.
    /// </summary>
    public string? ShowSaveFileDialog(string defaultName)
    {
        var filters = new (string Name, string[] Extensions)[]
        {
            ("JSON files", ["json"]),
        };
        var chosen = Window.ShowSaveFile("Save As", defaultName, filters);
        return string.IsNullOrEmpty(chosen) ? null : chosen;
    }

    public void ExitProcess() => Environment.Exit(0);

    private PhotinoWindow Window
        => _window ?? throw new InvalidOperationException("Photino window not attached yet; call Attach after building the host.");
}
