namespace EggLedger.Desktop.Platform;

public interface IDesktopWindow {
    (int Width, int Height) GetSize();

    string? ShowSaveFileDialog(string defaultName);

    void ExitProcess();

    string? ShowOpenFolderDialog();

    void SetSize(int width, int height);

    void SetFullScreen(bool fullScreen);
}
