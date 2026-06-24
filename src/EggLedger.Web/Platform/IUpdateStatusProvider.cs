namespace EggLedger.Web.Platform;

/// <summary>Phase of the desktop self-update flow the About overlay binds to. The browser build has no updater and stays permanently <see cref="UpToDate"/>.</summary>
public enum UpdatePhase {
    /// <summary>No update found / current build is latest (also the browser default).</summary>
    UpToDate,

    /// <summary>Polling the releases API.</summary>
    Checking,

    /// <summary>A newer version is available to install.</summary>
    Available,

    /// <summary>Downloading the new binary.</summary>
    Downloading,

    /// <summary>New binary downloaded; the new instance is taking over.</summary>
    Ready,

    /// <summary>The update attempt failed.</summary>
    Failed,
}

/// <summary>Read-only update-status surface the About overlay binds to. Browser registers a no-op; consumers gate display on <see cref="IPlatformCapabilities.IsDesktop"/>.</summary>
public interface IUpdateStatusProvider {
    /// <summary>Current update phase.</summary>
    UpdatePhase Phase { get; }

    /// <summary>Latest available version tag when <see cref="Phase"/> is Available/Ready, else null.</summary>
    string? AvailableVersion { get; }

    /// <summary>Human-readable release notes for the available version, when known.</summary>
    string? ReleaseNotes { get; }

    /// <summary>Bytes downloaded so far during the Downloading phase.</summary>
    long DownloadedBytes { get; }

    /// <summary>Total bytes to download (0 when unknown).</summary>
    long TotalBytes { get; }

    /// <summary>Last error / status message, when Phase is Failed or after a completed update.</summary>
    string? Message { get; }

    /// <summary>Raised whenever any field changes so the overlay re-renders.</summary>
    event Action? Changed;

    /// <summary>Poll for an update (no-op in the browser). When <paramref name="force"/> is false the desktop impl honors a 12h cooldown; an explicit user check passes true to bypass it.</summary>
    Task CheckForUpdatesAsync(bool force = false);

    /// <summary>Download + install the given tag (no-op in the browser). Drives Downloading -&gt; Ready/Failed.</summary>
    Task DownloadAndInstallAsync(string tag);
}

/// <summary>Browser no-op update-status provider: stays permanently <see cref="UpdatePhase.UpToDate"/> with no-op actions.</summary>
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
