using System.Globalization;

namespace EggLedger.Desktop.Update;

/// <summary>
/// File-system side of the self-replace: update lock, rename-with-retry, stale
/// cleanup. The download + new-instance launch + cross-process handoff live in
/// <see cref="UpdateService"/>; this holds only the testable file moves.
/// </summary>
public sealed class BinaryReplacement(IProcessProbe probe)
{
    /// <summary>Lock file name (in the exe dir). Matches Go updateLockFileName.</summary>
    public const string LockFileName = ".egg-update.lock";

    private readonly IProcessProbe _probe = probe;

    /// <summary>
    /// Rename src to dst, retrying to tolerate brief Windows file locks (antivirus,
    /// indexer) just after a process exits. Returns true on success.
    /// </summary>
    public static bool RenameWithRetry(string src, string dst, int attempts, TimeSpan delay)
    {
        for (var i = 0; i < attempts; i++)
        {
            try
            {
                // File.Move overwrite=true matches os.Rename's replace semantics.
                File.Move(src, dst, overwrite: true);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(delay);
            }
        }
        return false;
    }

    /// <summary>
    /// Create the lock file exclusively, writing the current PID; returns a release
    /// action and true on success. A live-PID lock returns (null, false); a stale
    /// (dead-PID) lock is reclaimed.
    /// </summary>
    public (Action? Release, bool Acquired) AcquireLock(string lockPath)
    {
        FileStream? TryCreate()
        {
            try
            {
                return new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        var f = TryCreate();
        if (f is null)
        {
            // Lock exists; check whether its owner is still alive.
            if (TryReadPid(lockPath, out var pid) && _probe.Exists(pid))
            {
                return (null, false);
            }
            // Stale or unreadable lock; reclaim it.
            TryDelete(lockPath);
            f = TryCreate();
            if (f is null)
            {
                return (null, false);
            }
        }

        using (f)
        {
            var pidBytes = System.Text.Encoding.UTF8.GetBytes(
                Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
            f.Write(pidBytes, 0, pidBytes.Length);
        }

        var released = false;
        void Release()
        {
            if (released)
            {
                return;
            }
            released = true;
            TryDelete(lockPath);
        }
        return (Release, true);
    }

    /// <summary>
    /// Remove leftover EggLedger*_new[.exe] files and a stale (dead-owner) lock in
    /// exeDir. Never deletes selfPath: a freshly launched _new instance matches the
    /// glob but still needs to rename itself.
    /// </summary>
    public void CleanStaleBinaries(string exeDir, string selfPath)
    {
        string[] patterns = ["EggLedger*_new", "EggLedger*_new.exe"];
        foreach (var pattern in patterns)
        {
            string[] matches;
            try
            {
                matches = Directory.GetFiles(exeDir, pattern);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
            {
                continue;
            }
            foreach (var match in matches)
            {
                if (SameFile(match, selfPath))
                {
                    continue;
                }
                TryDelete(match);
            }
        }

        var lockPath = Path.Combine(exeDir, LockFileName);
        if (TryReadPid(lockPath, out var pid))
        {
            if (!_probe.Exists(pid))
            {
                TryDelete(lockPath);
            }
        }
        else if (File.Exists(lockPath))
        {
            // Unparseable lock contents; remove (Go's else branch).
            TryDelete(lockPath);
        }
    }

    /// <summary>
    /// True when a and b resolve to the same on-disk file (falls back to abs-path
    /// compare on stat failure). Case-insensitive on Windows.
    /// </summary>
    public static bool SameFile(string a, string b)
    {
        try
        {
            var fa = new FileInfo(a);
            var fb = new FileInfo(b);
            if (fa.Exists && fb.Exists)
            {
                var comparison = OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;
                return string.Equals(fa.FullName, fb.FullName, comparison);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Fall through to abs-path compare.
        }

        try
        {
            var ra = Path.GetFullPath(a);
            var rb = Path.GetFullPath(b);
            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return string.Equals(ra, rb, comparison);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            return false;
        }
    }

    private static bool TryReadPid(string lockPath, out int pid)
    {
        pid = 0;
        try
        {
            var data = File.ReadAllText(lockPath).Trim();
            return int.TryParse(data, NumberStyles.Integer, CultureInfo.InvariantCulture, out pid);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort, matching Go's ignored os.Remove errors.
        }
    }
}
