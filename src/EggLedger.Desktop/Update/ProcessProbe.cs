using System.Diagnostics;

namespace EggLedger.Desktop.Update;

/// <summary>
/// Seam over checking whether a PID is still alive. Ports the per-OS
/// waitForProcessExit (EggLedger/update/update_windows.go uses WaitForSingleObject;
/// update_unix.go polls kill(pid, 0)). .NET's Process API is cross-platform, so a
/// single managed impl replaces both; the interface keeps it injectable for tests
/// (current process exists, a bogus PID does not).
/// </summary>
public interface IProcessProbe
{
    /// <summary>True if a process with this PID currently exists.</summary>
    bool Exists(int pid);
}

/// <summary>
/// Default cross-platform process probe. Mirrors the Go semantics: a PID we cannot
/// open / find is treated as already gone (Exists = false). On Windows a process
/// that has exited but not yet been reaped may still report HasExited; we honor
/// HasExited so a finished process is reported gone, matching the authoritative
/// WaitForSingleObject behavior the Go Windows path relies on.
/// </summary>
public sealed class ProcessProbe : IProcessProbe
{
    public bool Exists(int pid)
    {
        if (pid <= 0)
        {
            return false;
        }
        try
        {
            using var proc = Process.GetProcessById(pid);
            return !proc.HasExited;
        }
        catch (ArgumentException)
        {
            // No process with that id (already gone).
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}

/// <summary>
/// Polling wait-for-exit built on an <see cref="IProcessProbe"/>. Ports
/// waitForProcessExit: returns true once the process is gone, or false when the
/// timeout elapses while it is still alive. A zero timeout is a single immediate
/// probe (used by the lock-staleness check and stale-binary cleanup).
/// </summary>
public static class ProcessWait
{
    /// <summary>
    /// Block until <paramref name="pid"/> is gone or <paramref name="timeout"/>
    /// elapses. Returns true if the process has exited.
    /// </summary>
    public static bool WaitForExit(IProcessProbe probe, int pid, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            if (!probe.Exists(pid))
            {
                return true;
            }
            if (DateTime.UtcNow >= deadline)
            {
                return false;
            }
            Thread.Sleep(50);
        }
    }
}
