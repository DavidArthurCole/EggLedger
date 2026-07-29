namespace EggLedger.Web.Platform;

public enum UpdatePhase {
    UpToDate,

    Checking,

    Available,

    Downloading,

    Ready,

    Failed,
}

public interface IUpdateStatusProvider {
    UpdatePhase Phase { get; }

    string? AvailableVersion { get; }
    string? ReleaseNotes { get; }
    long DownloadedBytes { get; }

    long TotalBytes { get; }

    string? Message { get; }
    event Action? Changed;

    Task CheckForUpdatesAsync(bool force = false);

    Task DownloadAndInstallAsync(string tag);
}

public sealed class NoOpUpdateStatusProvider : IUpdateStatusProvider {
    public UpdatePhase Phase => UpdatePhase.UpToDate;
    public string? AvailableVersion => null;
    public string? ReleaseNotes => null;
    public long DownloadedBytes => 0;
    public long TotalBytes => 0;
    public string? Message => null;

    public event Action? Changed {
        add { }
        remove { }
    }

    public Task CheckForUpdatesAsync(bool force = false) => Task.CompletedTask;
    public Task DownloadAndInstallAsync(string tag) => Task.CompletedTask;
}
