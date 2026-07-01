using EggLedger.Desktop.Export;
using EggLedger.Domain.Export;
using EggLedger.Web.Platform;

namespace EggLedger.Desktop.Tests;

/// <summary>
/// DesktopDownloadService over a fake IPlatformCapabilities: the chosen path gets the
/// Domain export bytes (compared against the producers, not re-derived) and is revealed,
/// a cancelled dialog writes nothing. Real dialog/reveal are manual-verify.
/// </summary>
public sealed class DesktopDownloadServiceTests : IDisposable {
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), "egglg-dl-" + Guid.NewGuid().ToString("N"));

    public DesktopDownloadServiceTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() {
        try {
            Directory.Delete(_tempDir, recursive: true);
        } catch (DirectoryNotFoundException) {
        }
    }

    private sealed class FakePlatform(string? saveResult) : IPlatformCapabilities {
        public string? SaveResult { get; } = saveResult;
        public string? LastDefaultName { get; private set; }
        public List<string> Revealed { get; } = [];
        public bool IsDesktop => true;

        public Task<string?> ChooseSaveFilePathAsync(string defaultName) {
            LastDefaultName = defaultName;
            return Task.FromResult(SaveResult);
        }

        public Task OpenFileInFolderAsync(string path) {
            Revealed.Add(path);
            return Task.CompletedTask;
        }

        public Task OpenFileAsync(string path) => Task.CompletedTask;
        public Task OpenUrlAsync(string url) => Task.CompletedTask;
        public Task RestartAppAsync() => Task.CompletedTask;
        public Task<(int w, int h)> GetWindowSizeAsync() => Task.FromResult((0, 0));
        public Task<string?> ChooseFolderAsync() => Task.FromResult<string?>(null);
        public Task SetFolderHiddenAsync(string path, bool hidden) => Task.CompletedTask;
        public string DataRootDir => "";
    }

    private static IReadOnlyList<Mission> SampleMissions() => [];

    [Fact]
    public async Task DownloadCsvAsync_WritesDomainBytesToChosenPath_AndReveals() {
        var missions = SampleMissions();
        var path = Path.Combine(_tempDir, "out.csv");
        var platform = new FakePlatform(path);
        var sink = new DesktopDownloadService(platform);

        await sink.DownloadCsvAsync(missions, "account.csv");

        Assert.True(File.Exists(path));
        Assert.Equal(MissionExport.MissionsToCsvBytes(missions), await File.ReadAllBytesAsync(path));
        Assert.Equal("account.csv", platform.LastDefaultName);
        Assert.Equal(path, Assert.Single(platform.Revealed));
    }

    [Fact]
    public async Task DownloadXlsxAsync_WritesDomainBytesToChosenPath_AndReveals() {
        var missions = SampleMissions();
        var path = Path.Combine(_tempDir, "out.xlsx");
        var platform = new FakePlatform(path);
        var sink = new DesktopDownloadService(platform);

        await sink.DownloadXlsxAsync(missions, "account.xlsx");

        Assert.True(File.Exists(path));
        Assert.Equal(MissionExport.MissionsToXlsxBytes(missions), await File.ReadAllBytesAsync(path));
        Assert.Equal(path, Assert.Single(platform.Revealed));
    }

    [Fact]
    public async Task DownloadCsvAsync_Cancelled_WritesNothing_AndDoesNotReveal() {
        var platform = new FakePlatform(saveResult: null);
        var sink = new DesktopDownloadService(platform);

        await sink.DownloadCsvAsync(SampleMissions(), "account.csv");

        Assert.Empty(Directory.GetFiles(_tempDir));
        Assert.Empty(platform.Revealed);
    }

    [Fact]
    public async Task DownloadXlsxAsync_Cancelled_WritesNothing_AndDoesNotReveal() {
        var platform = new FakePlatform(saveResult: null);
        var sink = new DesktopDownloadService(platform);

        await sink.DownloadXlsxAsync(SampleMissions(), "account.xlsx");

        Assert.Empty(Directory.GetFiles(_tempDir));
        Assert.Empty(platform.Revealed);
    }
}
