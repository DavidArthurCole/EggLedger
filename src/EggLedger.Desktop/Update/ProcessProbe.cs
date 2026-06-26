using System.Diagnostics;

namespace EggLedger.Desktop.Update;

/// <summary>Seam over checking whether a PID is still alive. A single managed Process-based impl replaces the Go per-OS waitForProcessExit; the interface keeps it injectable for tests.</summary>
public interface IProcessProbe {
    /// <summary>True if a process with this PID currently exists.</summary>
    bool Exists(int pid);
}

/// <summary>Default cross-platform process probe. A PID we cannot open/find is treated as gone (Exists = false); HasExited is honored so a finished-but-unreaped process reports gone, matching the Go WaitForSingleObject behavior.</summary>
public sealed class ProcessProbe : IProcessProbe {
    public bool Exists(int pid) {
        if (pid <= 0) {
            return false;
        }
        try {
            using var proc = Process.GetProcessById(pid);
            return !proc.HasExited;
        } catch (ArgumentException) {
            // No process with that id (already gone).
            return false;
        } catch (InvalidOperationException) {
            return false;
        }
    }
}

/// <summary>Polling wait-for-exit over an <see cref="IProcessProbe"/>. Returns true once the process is gone, false on timeout while still alive; a zero timeout is a single immediate probe.</summary>
public static class ProcessWait {
    /// <summary>Block until <paramref name="pid"/> is gone or <paramref name="timeout"/> elapses. Returns true if the process has exited.</summary>
    public static bool WaitForExit(IProcessProbe probe, int pid, TimeSpan timeout) {
        var deadline = DateTime.UtcNow + timeout;
        while (true) {
            if (!probe.Exists(pid)) {
                return true;
            }
            if (DateTime.UtcNow >= deadline) {
                return false;
            }
            Thread.Sleep(50);
        }
    }
}
