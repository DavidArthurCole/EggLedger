using System.Reflection;
using EggLedger.Desktop.Export;
using EggLedger.Desktop.Platform;
using EggLedger.Desktop.Storage;
using EggLedger.Desktop.Update;
using EggLedger.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Photino.Blazor;

// Photino desktop host: native window + WebView running the shared EggLedger.Web
// Blazor UI directly (BlazorWebView model). .NET replacement for the Go/lorca shell.

// D4 self-update startup: clean stale _new binaries and, if launched as
// EggLedger_new with --replace-* flags, run the takeover (ping old instance, wait
// for exit, rename to canonical EggLedger[.exe]). Runs before the window so the
// on-disk binary is canonical by first paint.
// MANUAL-VERIFY: the cross-process takeover only runs in a real second process.
var updateBootstrap = new UpdateBootstrap(new ProcessProbe(), new BinaryReplacement(new ProcessProbe()));
updateBootstrap.RunStartup(args, Environment.ProcessPath);

var appBuilder = PhotinoBlazorAppBuilder.CreateDefault(args);

// Reuse the shared UI service set; the desktop swaps in native storage (D2),
// platform caps (D3), updater (D4), and save-to-disk export (D5) below. Base
// address is the production cloud-sync origin (desktop has no local server);
// CloudSyncService issues relative "api/v1/..." against it.
// MANUAL-VERIFY: confirm this matches the deployed sync server origin.
appBuilder.Services.AddEggLedgerWeb(CloudSyncBaseAddress());

// D2 native SQLite storage: replaces browser IndexedDB stores and the report path
// with live SQL ReportExecutor. Data lives under the exe data root (honors
// bootstrap.json relocation). Must run after AddEggLedgerWeb so overrides win.
var dataRootDir = StoragePaths.ResolveDataRootDir(AppContext.BaseDirectory);
appBuilder.Services.AddDesktopSqliteStorage(dataRootDir);

// D3 native platform capabilities (open/reveal file, save dialog, restart, window
// size). The Photino window only exists after Build, so the window seam is a
// late-bound holder attached below. Must run after AddEggLedgerWeb.
var desktopWindow = new PhotinoDesktopWindow();
appBuilder.Services.AddDesktopPlatformCapabilities(new ProcessRunner(), desktopWindow);

// D4 live self-update status provider for the About overlay. Running version is
// the assembly informational version (falls back to file version). The default
// exit action (Environment.Exit) is the OLD-instance exit after the new instance
// reports /ready, letting it rename EggLedger_new -> EggLedger.
var runningVersion = DesktopAppVersion();
appBuilder.Services.AddDesktopUpdater(() => runningVersion);

// D5 desktop export sink: writes export bytes to a path picked via the native save
// dialog (D3) and reveals the file. Must run after AddEggLedgerWeb and the platform
// caps registration (it depends on them).
appBuilder.Services.AddDesktopExportSink();

// Host page is desktop.html (not index.html) to avoid colliding in the
// static-web-assets manifest with the referenced WASM project's index.html.
appBuilder.Services.Configure<PhotinoBlazorAppConfiguration>(opts => opts.HostPage = "desktop.html");

appBuilder.RootComponents.Add<App>("#app");

var app = appBuilder.Build();

// Bind the built window to the late-bound seam so GetWindowSize and the save
// dialog operate on the real window.
desktopWindow.Attach(app.MainWindow);

// Window config ported from the Go/lorca setup. The Go build read size from stored
// user prefs; for now use a default and center.
const int defaultWidth = 1280;
const int defaultHeight = 800;

app.MainWindow
    .SetTitle("EggLedger")
    .SetIconFile("icon-64.png")
    .SetUseOsDefaultSize(false)
    .SetSize(defaultWidth, defaultHeight)
    .SetUseOsDefaultLocation(false)
    .Center();

AppDomain.CurrentDomain.UnhandledException += (_, error) => {
    app.MainWindow.ShowMessage("Fatal exception", error.ExceptionObject.ToString() ?? "Unknown error");
};

app.Run();

// Production cloud-sync host. Trailing slash required so relative "api/v1/..."
// URIs resolve under the host root.
static Uri CloudSyncBaseAddress() => new("https://ledgersync.davidarthurcole.me/");

// EggLedger product version for the updater compare. Prefer the assembly
// informational version (strip any +sha suffix), fall back to a default.
static string DesktopAppVersion() {
    var info = Assembly.GetEntryAssembly()?
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
    if (!string.IsNullOrEmpty(info)) {
        var plus = info.IndexOf('+', StringComparison.Ordinal);
        return plus >= 0 ? info[..plus] : info;
    }
    return "2.1.4";
}
