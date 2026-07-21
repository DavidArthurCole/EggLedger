using System.Globalization;

namespace EggLedger.Desktop.Update;

public sealed class BinaryReplacement(IProcessProbe probe) {
    public const string LockFileName = ".egg-update.lock";

    private readonly IProcessProbe _probe = probe;

    public static bool RenameWithRetry(string src, string dst, int attempts, TimeSpan delay) {
        for (var i = 0; i < attempts; i++) {
            try {

                File.Move(src, dst, overwrite: true);
                return true;
            } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                Thread.Sleep(delay);
            }
        }
        return false;
    }

    public (Action? Release, bool Acquired) AcquireLock(string lockPath) {
        FileStream? TryCreate() {
            try {
                return new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            } catch (IOException) {
                return null;
            } catch (UnauthorizedAccessException) {
                return null;
            }
        }

        var f = TryCreate();
        if (f is null) {

            if (TryReadPid(lockPath, out var pid) && _probe.Exists(pid)) {
                return (null, false);
            }

            TryDelete(lockPath);
            f = TryCreate();
            if (f is null) {
                return (null, false);
            }
        }

        using (f) {
            var pidBytes = System.Text.Encoding.UTF8.GetBytes(
                Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
            f.Write(pidBytes, 0, pidBytes.Length);
        }

        var released = false;
        void Release() {
            if (released) {
                return;
            }
            released = true;
            TryDelete(lockPath);
        }
        return (Release, true);
    }

    public void CleanStaleBinaries(string exeDir, string selfPath) {
        string[] patterns = [$"EggLedger*{BinaryNaming.NewBinarySuffix}", $"EggLedger*{BinaryNaming.NewBinarySuffix}.exe"];
        foreach (var pattern in patterns) {
            string[] matches;
            try {
                matches = Directory.GetFiles(exeDir, pattern);
            } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException) {
                continue;
            }
            foreach (var match in matches) {
                if (SameFile(match, selfPath)) {
                    continue;
                }
                TryDelete(match);
            }
        }

        var lockPath = Path.Combine(exeDir, LockFileName);
        if (TryReadPid(lockPath, out var pid)) {
            if (!_probe.Exists(pid)) {
                TryDelete(lockPath);
            }
        } else if (File.Exists(lockPath)) {

            TryDelete(lockPath);
        }
    }

    public static bool SameFile(string a, string b) {
        try {
            var fa = new FileInfo(a);
            var fb = new FileInfo(b);
            if (fa.Exists && fb.Exists) {
                var comparison = OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;
                return string.Equals(fa.FullName, fb.FullName, comparison);
            }
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) {

        }

        try {
            var ra = Path.GetFullPath(a);
            var rb = Path.GetFullPath(b);
            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return string.Equals(ra, rb, comparison);
        } catch (Exception ex) when (ex is ArgumentException or IOException) {
            return false;
        }
    }

    private static bool TryReadPid(string lockPath, out int pid) {
        pid = 0;
        try {
            var data = File.ReadAllText(lockPath).Trim();
            return int.TryParse(data, NumberStyles.Integer, CultureInfo.InvariantCulture, out pid);
        } catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or IOException or UnauthorizedAccessException) {
            return false;
        }
    }

    internal static void TryDelete(string path) {
        try {
            File.Delete(path);
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
        }
    }
}
