using System.Runtime.InteropServices;

namespace EggLedger.Desktop.Platform;

/// <summary>
/// Pure per-OS command construction for the shell-out platform operations (open a
/// file with the default handler, reveal a file in its folder, relaunch the exe).
/// Kept side-effect free so all three platform branches are unit-testable by
/// passing the target <see cref="OSPlatform"/> explicitly. Ports the executables
/// and arguments from the Go reference (EggLedger/platform/platform*.go).
/// </summary>
public static class DesktopCommandBuilder
{
    /// <summary>
    /// Command to open <paramref name="path"/> with the OS default handler.
    /// Windows: explorer &lt;path&gt;. macOS: open &lt;path&gt;. Linux: xdg-open &lt;path&gt;.
    /// </summary>
    public static (string Exe, string[] Args) BuildOpenCommand(OSPlatform platform, string path)
    {
        if (platform == OSPlatform.Windows)
        {
            return ("explorer.exe", [path]);
        }
        if (platform == OSPlatform.OSX)
        {
            return ("open", [path]);
        }
        return ("xdg-open", [path]);
    }

    /// <summary>
    /// Command to reveal <paramref name="path"/> in the OS file browser.
    /// Windows: explorer /select,&lt;path&gt; (single combined argument, matching the
    /// Go ShellExecute parameters). macOS: open -R &lt;path&gt;. Linux: xdg-open on the
    /// containing directory, since file selection is file-manager specific (Go does
    /// the same via filepath.Dir).
    /// </summary>
    public static (string Exe, string[] Args) BuildOpenInFolderCommand(OSPlatform platform, string path)
    {
        if (platform == OSPlatform.Windows)
        {
            return ("explorer.exe", ["/select," + path]);
        }
        if (platform == OSPlatform.OSX)
        {
            return ("open", ["-R", path]);
        }
        var dir = Path.GetDirectoryName(path) ?? path;
        return ("xdg-open", [dir]);
    }

    /// <summary>
    /// Command to relaunch the desktop executable at <paramref name="exePath"/>. On
    /// every platform the exe is invoked directly with no arguments; the caller
    /// exits the current process after launching it (the restart-relaunch primitive
    /// used by the self-update flow).
    /// </summary>
    public static (string Exe, string[] Args) BuildRestartCommand(string exePath)
        => (exePath, []);
}
