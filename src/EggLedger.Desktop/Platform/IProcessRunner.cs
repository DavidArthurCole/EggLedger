namespace EggLedger.Desktop.Platform;

/// <summary>
/// Seam over launching an external process. Tests inject a fake recording the
/// (exe, args) so per-OS command construction is assertable without launching.
/// </summary>
public interface IProcessRunner {
    /// <summary>Start exe with args, fire-and-forget (like Go exec.Command.Start).</summary>
    Task RunAsync(string exe, IReadOnlyList<string> args);
}
