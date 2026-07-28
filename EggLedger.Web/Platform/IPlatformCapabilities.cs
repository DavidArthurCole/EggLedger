namespace EggLedger.Web.Platform;

public interface IPlatformCapabilities {
    bool IsDesktop { get; }

    Task OpenFileAsync(string path);

    Task OpenUrlAsync(string url);

    Task OpenFileInFolderAsync(string path);

    Task<string?> ChooseSaveFilePathAsync(string defaultName);

    Task RestartAppAsync();
    Task<(int w, int h)> GetWindowSizeAsync();

    Task<string?> ChooseFolderAsync();

    Task SetFolderHiddenAsync(string path, bool hidden);

    string DataRootDir { get; }
}
