namespace EggLedger.Desktop.Update;

/// <summary>
/// Startup update plumbing run before the UI: parse --replace-* / --handshake-*
/// flags, clean stale binaries, and (when this process is the launched
/// EggLedger_new) run the takeover that renames itself to canonical EggLedger[.exe]
/// once the old instance exits.
/// MANUAL-VERIFY: the takeover only runs in a real second process; the pieces it
/// calls are unit-tested but the two-process orchestration is verified by hand.
/// </summary>
public sealed class UpdateBootstrap(IProcessProbe probe, BinaryReplacement replacement) {
    private readonly IProcessProbe _probe = probe;
    private readonly BinaryReplacement _replacement = replacement;

    /// <summary>Parsed --replace-* / --handshake-* flags.</summary>
    public readonly record struct ReplaceArgs(int ReplacePid, string ReplacePath, string HandshakePort, string HandshakeToken);

    /// <summary>
    /// Parse the updater flags out of argv. Accepts both "--flag=value" and
    /// "--flag value" forms. Unknown args are ignored (Photino consumes the rest).
    /// </summary>
    public static ReplaceArgs ParseArgs(string[] args) {
        var pid = 0;
        var path = "";
        var port = "";
        var token = "";

        for (var i = 0; i < args.Length; i++) {
            var (key, value) = SplitArg(args, ref i);
            switch (key) {
                case "--replace-pid":
                    _ = int.TryParse(value, out pid);
                    break;
                case "--replace-path":
                    path = value;
                    break;
                case "--handshake-port":
                    port = value;
                    break;
                case "--handshake-token":
                    token = value;
                    break;
            }
        }

        return new ReplaceArgs(pid, path, port, token);
    }

    private static (string Key, string Value) SplitArg(string[] args, ref int i) {
        var arg = args[i];
        var eq = arg.IndexOf('=', StringComparison.Ordinal);
        if (eq >= 0) {
            return (arg[..eq], arg[(eq + 1)..]);
        }
        // "--flag value" form: consume the next token as the value when present.
        if (arg.StartsWith("--", StringComparison.Ordinal) && i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)) {
            i++;
            return (arg, args[i]);
        }
        return (arg, "");
    }

    /// <summary>
    /// Run startup cleanup, then takeover if in replace mode. Returns true if this
    /// process acted as the takeover (renamed itself / is the new instance).
    /// </summary>
    public bool RunStartup(string[] args, string? selfPath) {
        var self = selfPath ?? Environment.ProcessPath ?? "";
        var exeDir = string.IsNullOrEmpty(self) ? "" : (Path.GetDirectoryName(self) ?? "");

        // Always clean stale leftovers first (never our own running image).
        if (!string.IsNullOrEmpty(exeDir)) {
            _replacement.CleanStaleBinaries(exeDir, self);
        }

        var parsed = ParseArgs(args);
        var (run, oldPid, oldPath) = BinaryNaming.DecideReplace(self, parsed.ReplacePid, parsed.ReplacePath);
        if (!run) {
            return false;
        }

        RunTakeover(self, oldPid, oldPath, parsed.HandshakePort, parsed.HandshakeToken);
        return true;
    }

    /// <summary>
    /// New-instance takeover: ping the old instance, wait for it to exit, then
    /// rename self to the canonical path. Ports runTakeover. The ping uses a
    /// short-lived HttpClient; failures are tolerated (best-effort, like Go).
    /// </summary>
    public void RunTakeover(string self, int oldPid, string oldPath, string handshakePort, string handshakeToken) {
        if (!string.IsNullOrEmpty(handshakePort) && !string.IsNullOrEmpty(handshakeToken)) {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var client = new HandshakeClient(http);
            client.PingOldReadyAsync(handshakePort, handshakeToken).GetAwaiter().GetResult();
        }

        if (oldPid > 0) {
            ProcessWait.WaitForExit(_probe, oldPid, TimeSpan.FromSeconds(30));
        }

        if (string.IsNullOrEmpty(self) || string.IsNullOrEmpty(oldPath)) {
            return;
        }

        var exeDir = Path.GetDirectoryName(oldPath) ?? "";
        var (release, ok) = _replacement.AcquireLock(Path.Combine(exeDir, BinaryReplacement.LockFileName));
        if (!ok) {
            return;
        }
        try {
            if (BinaryReplacement.SameFile(self, oldPath)) {
                // Already canonical (named-launch where we are EggLedger.exe): nothing to do.
                return;
            }

            // More attempts/longer delay when we did not know the old PID (name-only
            // launch), matching Go's 60 * 500ms vs 10 * 300ms.
            var attempts = oldPid == 0 ? 60 : 10;
            var delay = oldPid == 0 ? TimeSpan.FromMilliseconds(500) : TimeSpan.FromMilliseconds(300);
            BinaryReplacement.RenameWithRetry(self, oldPath, attempts, delay);
        } finally {
            release?.Invoke();
        }
    }
}
