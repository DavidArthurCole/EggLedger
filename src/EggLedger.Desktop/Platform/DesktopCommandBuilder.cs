using System.Runtime.InteropServices;

namespace EggLedger.Desktop.Platform;

/// <summary>Pure per-OS command construction for shell-out platform ops (open, reveal, relaunch).</summary>
/// <remarks>Side-effect free so all three platform branches are unit-testable by passing the target
/// <see cref="OSPlatform"/>. Ports Go platform/platform*.go.</remarks>
public static class DesktopCommandBuilder {
    /// <summary>Command to open <paramref name="path"/> with the OS default handler.</summary>
    /// <remarks>Windows: explorer &lt;path&gt;. macOS: open &lt;path&gt;. Linux: xdg-open &lt;path&gt;.</remarks>
    public static (string Exe, string[] Args) BuildOpenCommand(OSPlatform platform, string path) {
        if (platform == OSPlatform.Windows) {
            return ("explorer.exe", [path]);
        }
        if (platform == OSPlatform.OSX) {
            return ("open", [path]);
        }
        return ("xdg-open", [path]);
    }

    /// <summary>Command to reveal path in the OS file browser.</summary>
    /// <remarks>Windows: explorer /select,path (single combined arg). macOS: open -R. Linux:
    /// xdg-open on the containing dir (file selection is file-manager specific).</remarks>
    public static (string Exe, string[] Args) BuildOpenInFolderCommand(OSPlatform platform, string path) {
        if (platform == OSPlatform.Windows) {
            return ("explorer.exe", ["/select," + path]);
        }
        if (platform == OSPlatform.OSX) {
            return ("open", ["-R", path]);
        }
        var dir = Path.GetDirectoryName(path) ?? path;
        return ("xdg-open", [dir]);
    }

    /// <summary>Command to relaunch the exe at exePath (invoked directly, no args).</summary>
    /// <remarks>Caller exits the current process after launching it.</remarks>
    public static (string Exe, string[] Args) BuildRestartCommand(string exePath)
        => (exePath, []);
}
