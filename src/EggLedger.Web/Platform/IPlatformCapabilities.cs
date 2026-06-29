namespace EggLedger.Web.Platform;

/// <summary>Seam over platform-specific shell features. The browser build is no-ops; desktop-only UI gates on <see cref="IsDesktop"/>.</summary>
public interface IPlatformCapabilities {
    /// <summary>True in a desktop shell with real OS file access.</summary>
    bool IsDesktop { get; }

    /// <summary>Open with the OS default handler.</summary>
    Task OpenFileAsync(string path);

    /// <summary>Reveal in the OS file browser (select it in its folder).</summary>
    Task OpenFileInFolderAsync(string path);

    /// <summary>Returns the chosen absolute path, or null when no native picker is available.</summary>
    Task<string?> ChooseSaveFilePathAsync(string defaultName);

    /// <summary>Desktop self-update flow.</summary>
    Task RestartAppAsync();
    Task<(int w, int h)> GetWindowSizeAsync();

    /// <summary>Native folder picker; returns the chosen path or null (null in the browser).</summary>
    Task<string?> ChooseFolderAsync();

    /// <summary>Toggle the OS hidden attribute on a folder (no-op in the browser and on Linux).</summary>
    Task SetFolderHiddenAsync(string path, bool hidden);

    /// <summary>Absolute data-root dir on desktop; empty string in the browser.</summary>
    string DataRootDir { get; }
}
