namespace EggLedger.Desktop.Platform;

/// <summary>
/// Seam over launching an external process. The real impl shells out via
/// <see cref="System.Diagnostics.Process"/>; tests inject a fake that records the
/// (exe, args) it was asked to run so the per-OS command construction can be
/// asserted without launching anything.
/// </summary>
public interface IProcessRunner
{
    /// <summary>Start <paramref name="exe"/> with <paramref name="args"/> (fire-and-forget, like Go exec.Command.Start).</summary>
    Task RunAsync(string exe, IReadOnlyList<string> args);
}
