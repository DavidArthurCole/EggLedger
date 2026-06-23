using System.Diagnostics;

namespace EggLedger.Desktop.Platform;

/// <summary>
/// Real <see cref="IProcessRunner"/>: starts the process detached and does not wait
/// for it, mirroring the Go reference which uses exec.Command(...).Start() for the
/// open/reveal shell-outs. UseShellExecute is false so args are passed verbatim.
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    public Task RunAsync(string exe, IReadOnlyList<string> args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = false,
        };
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }
        Process.Start(psi);
        return Task.CompletedTask;
    }
}
