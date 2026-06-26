namespace EggLedger.Web.Platform;

/// <summary>Phase of the desktop self-update flow. The browser build has no updater and stays permanently <see cref="UpToDate"/>.</summary>
public enum UpdatePhase {
    /// <summary>Current build is latest (also the browser default).</summary>
    UpToDate,

    Checking,

    Available,

    Downloading,

    /// <summary>New binary downloaded; the new instance is taking over.</summary>
    Ready,

    Failed,
}

/// <summary>Read-only update-status surface for the About overlay. Browser registers a no-op; consumers gate display on <see cref="IPlatformCapabilities.IsDesktop"/>.</summary>
public interface IUpdateStatusProvider {
    UpdatePhase Phase { get; }

    /// <summary>Latest version tag when <see cref="Phase"/> is Available/Ready, else null.</summary>
    string? AvailableVersion { get; }

    string? ReleaseNotes { get; }

    long DownloadedBytes { get; }

    /// <summary>0 when unknown.</summary>
    long TotalBytes { get; }

    /// <summary>Last error / status, when Phase is Failed or after a completed update.</summary>
    string? Message { get; }

    event Action? Changed;

    /// <summary>No-op in the browser. <paramref name="force"/> false honors the desktop 12h cooldown; an explicit user check passes true to bypass it.</summary>
    Task CheckForUpdatesAsync(bool force = false);

    /// <summary>No-op in the browser. Drives Downloading -&gt; Ready/Failed.</summary>
    Task DownloadAndInstallAsync(string tag);
}

/// <summary>Browser no-op: stays permanently <see cref="UpdatePhase.UpToDate"/>.</summary>
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
