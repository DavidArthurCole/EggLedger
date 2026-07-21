using System.Diagnostics;

namespace EggLedger.Desktop.Update;

public interface IProcessProbe {
    bool Exists(int pid);
}

public sealed class ProcessProbe : IProcessProbe {
    public bool Exists(int pid) {
        if (pid <= 0) {
            return false;
        }
        try {
            using var proc = Process.GetProcessById(pid);
            return !proc.HasExited;
        } catch (ArgumentException) {

            return false;
        } catch (InvalidOperationException) {
            return false;
        }
    }
}

public static class ProcessWait {
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
