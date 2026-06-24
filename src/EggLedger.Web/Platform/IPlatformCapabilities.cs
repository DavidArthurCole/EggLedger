namespace EggLedger.Web.Platform;

/// <summary>Capability seam over platform-specific shell features. The browser build implements these as downloads/no-ops; desktop-only UI gates on <see cref="IsDesktop"/>.</summary>
public interface IPlatformCapabilities {
    /// <summary>True when running in a desktop shell with real OS file access.</summary>
    bool IsDesktop { get; }

    /// <summary>Open a file with the OS default handler.</summary>
    Task OpenFileAsync(string path);

    /// <summary>Reveal a file in the OS file browser (select it in its folder).</summary>
    Task OpenFileInFolderAsync(string path);

    /// <summary>Prompt for a save path; returns the chosen absolute path, or null when no native picker is available.</summary>
    Task<string?> ChooseSaveFilePathAsync(string defaultName);

    /// <summary>Restart the application (desktop self-update flow).</summary>
    Task RestartAppAsync();

    /// <summary>Current window size in pixels.</summary>
    Task<(int w, int h)> GetWindowSizeAsync();
}
