namespace EggLedger.Desktop.Platform;

public interface IProcessRunner {
    Task RunAsync(string exe, IReadOnlyList<string> args);
}
