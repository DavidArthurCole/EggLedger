using EggLedger.Desktop.Export;
using EggLedger.Desktop.Platform;
using EggLedger.Desktop.Storage;
using EggLedger.Desktop.Update;
using EggLedger.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Photino.Blazor;

// Photino desktop host: native window + WebView running the shared EggLedger.Web
// Blazor UI directly. .NET replacement for the Go/lorca shell.

// D4 self-update startup: clean stale _new binaries and run the takeover if launched
// as EggLedger_new. Runs before the window so the on-disk binary is canonical by first paint.
// MANUAL-VERIFY: the cross-process takeover only runs in a real second process.
var updateBootstrap = new UpdateBootstrap(new ProcessProbe(), new BinaryReplacement(new ProcessProbe()));
updateBootstrap.RunStartup(args, Environment.ProcessPath);

// Single-file packaging: the RCL static web assets do not fold into the exe, so the build
// embeds them as a wwwroot.zip assembly resource. Photino's default file provider reads
// BaseDir/wwwroot, so extract the resource there before the window builds.
EnsureWwwrootExtracted();

var appBuilder = PhotinoBlazorAppBuilder.CreateDefault(args);

// Shared UI service set; desktop swaps in native storage (D2), platform caps (D3),
// updater (D4), and export (D5) below. Base address is the production cloud-sync origin.
// MANUAL-VERIFY: confirm this matches the deployed sync server origin.
appBuilder.Services.AddEggLedgerWeb(CloudSyncBaseAddress());

// D2 native SQLite storage: replaces browser IndexedDB with a live SQL ReportExecutor.
// Data lives under the exe data root (honors bootstrap.json relocation); must run after
// AddEggLedgerWeb so overrides win.
var dataRootDir = StoragePaths.ResolveDataRootDir(AppContext.BaseDirectory);
appBuilder.Services.AddDesktopSqliteStorage(dataRootDir);

// D3 native platform capabilities (open/reveal file, save dialog, restart, window size).
// The Photino window only exists after Build, so the window seam is a late-bound holder
// attached below; must run after AddEggLedgerWeb.
var desktopWindow = new PhotinoDesktopWindow();
appBuilder.Services.AddDesktopPlatformCapabilities(new ProcessRunner(), desktopWindow);

// D4 live self-update status provider for the About overlay; running version is the
// assembly informational version. The default exit action is the OLD-instance exit after
// the new instance reports /ready, letting it rename EggLedger_new -> EggLedger.
var runningVersion = AppVersionInfo.Current;
appBuilder.Services.AddDesktopUpdater(() => runningVersion);

// D5 desktop export sink: writes export bytes to a path picked via the native save dialog
// (D3) and reveals the file. Must run after AddEggLedgerWeb and the platform caps registration.
appBuilder.Services.AddDesktopExportSink();

// Host page is desktop.html (not index.html) to avoid colliding in the static-web-assets
// manifest with the referenced WASM project's index.html.
appBuilder.Services.Configure<PhotinoBlazorAppConfiguration>(opts => opts.HostPage = "desktop.html");

appBuilder.RootComponents.Add<App>("#app");

var app = appBuilder.Build();

// Bind the built window to the late-bound seam so GetWindowSize and the save dialog
// operate on the real window.
desktopWindow.Attach(app.MainWindow);

// Window config ported from the Go/lorca setup. The Go build read size from stored user
// prefs; for now use a default and center.
const int defaultWidth = 1280;
const int defaultHeight = 800;

// Icon ships inside the single-file exe and self-extracts to BaseDir; SetIconFile
// resolves relative paths against the working dir, so pass an absolute path and skip
// it when absent rather than letting Photino throw at WaitForClose.
var iconFile = Path.Combine(AppContext.BaseDirectory, "icon-64.png");
app.MainWindow.SetTitle("EggLedger");
if (File.Exists(iconFile)) app.MainWindow.SetIconFile(iconFile);
app.MainWindow
    .SetUseOsDefaultSize(false)
    .SetSize(defaultWidth, defaultHeight)
    .SetUseOsDefaultLocation(false)
    .Center();

// Calling Photino's native ShowMessage from the unhandled-exception path access-
// violates, so log the fault to stderr and a file beside the exe instead.
AppDomain.CurrentDomain.UnhandledException += (_, error) => {
    var text = error.ExceptionObject.ToString() ?? "Unknown error";
    Console.Error.WriteLine("FATAL: " + text);
    try {
        var logDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        File.AppendAllText(Path.Combine(logDir, "EggLedger.fatal.log"), text + Environment.NewLine);
    } catch { }
};

app.Run();

// Trailing slash required so relative "api/v1/..." URIs resolve under the host root.
static Uri CloudSyncBaseAddress() => new("https://ledgersync.davidarthurcole.me/");

// Extract the embedded wwwroot.zip resource to BaseDir/wwwroot. Stamps with the assembly
// MVID so a swapped-in update (self-updater) re-extracts; a matching stamp skips the work.
// When the resource is absent (loose dev/publish wwwroot) the existing tree is left alone.
static void EnsureWwwrootExtracted() {
    var assembly = System.Reflection.Assembly.GetExecutingAssembly();
    using var zipStream = assembly.GetManifestResourceStream("EggLedger.Desktop.wwwroot.zip");
    if (zipStream is null) return;
    var wwwrootDir = Path.Combine(AppContext.BaseDirectory, "wwwroot");
    var stamp = Path.Combine(wwwrootDir, ".pack-stamp");
    var mvid = assembly.ManifestModule.ModuleVersionId.ToString();
    if (File.Exists(stamp) && File.ReadAllText(stamp) == mvid) return;
    if (Directory.Exists(wwwrootDir)) Directory.Delete(wwwrootDir, recursive: true);
    Directory.CreateDirectory(wwwrootDir);
    using (var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Read)) {
        System.IO.Compression.ZipFileExtensions.ExtractToDirectory(archive, wwwrootDir, overwriteFiles: true);
    }
    File.WriteAllText(stamp, mvid);
}
