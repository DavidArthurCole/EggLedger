using Photino.NET;

namespace EggLedger.Desktop.Platform;

/// <summary>Real <see cref="IDesktopWindow"/> backed by the Photino window.</summary>
/// <remarks>The window is bound late via <see cref="Attach"/> because the MainWindow only exists
/// after the host is built, but DI is wired before build. MANUAL-VERIFY: the save dialog and exit
/// cannot be exercised headlessly.</remarks>
public sealed class PhotinoDesktopWindow : IDesktopWindow {
    public PhotinoDesktopWindow() {
    }

    public PhotinoDesktopWindow(PhotinoWindow window) => Window = window;

    /// <summary>Bind the Photino window once the host has built it.</summary>
    public void Attach(PhotinoWindow window) => Window = window;

    public (int Width, int Height) GetSize() {
        var w = Window;
        return (w.Width, w.Height);
    }

    /// <summary>
    /// MANUAL-VERIFY: Photino native save dialog filtered to JSON. Returns null on cancel.
    /// </summary>
    public string? ShowSaveFileDialog(string defaultName) {
        var filters = new (string Name, string[] Extensions)[]
        {
            ("JSON files", ["json"]),
        };
        var chosen = Window.ShowSaveFile("Save As", defaultName, filters);
        return string.IsNullOrEmpty(chosen) ? null : chosen;
    }

    public void ExitProcess() => Environment.Exit(0);

    private PhotinoWindow Window { get => field ?? throw new InvalidOperationException("Photino window not attached yet; call Attach after building the host."); set; }
}
