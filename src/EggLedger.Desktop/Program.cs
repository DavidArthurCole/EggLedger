using System.Reflection;
using EggLedger.Desktop.Export;
using EggLedger.Desktop.Platform;
using EggLedger.Desktop.Storage;
using EggLedger.Desktop.Update;
using EggLedger.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Photino.Blazor;

// EggLedger Photino desktop host. Native OS window + system WebView hosting the
// shared EggLedger.Web Blazor UI. This is the .NET-native replacement for the
// Go/lorca shell (see EggLedger/main.go). It runs the Blazor components in the
// WebView directly (BlazorWebView model), not as separately served WASM.

// D4 - self-update startup plumbing. Clean stale _new binaries and, if this
// process was launched as EggLedger_new with the --replace-* handshake flags, run
// the takeover: ping the old instance, wait for it to exit, rename ourselves into
// the canonical EggLedger[.exe]. Runs before the window comes up so the on-disk
// binary is canonical by the time the UI is shown.
// MANUAL-VERIFY: the cross-process takeover only exercises in a real second
// process spawned after a download; the unit-tested pieces back it.
var updateBootstrap = new UpdateBootstrap(new ProcessProbe(), new BinaryReplacement(new ProcessProbe()));
updateBootstrap.RunStartup(args, Environment.ProcessPath);

var appBuilder = PhotinoBlazorAppBuilder.CreateDefault(args);

// Reuse the shared UI service set. The C2 services are host-agnostic over
// HttpClient/JSInterop, and the desktop swaps in native storage (D2), platform
// caps (D3), the updater (D4), and the save-to-disk export sink (D5) below.
//
// The HttpClient base address is the cloud-sync / Egg-API origin. On the browser
// it is same-origin; on desktop there is no local server, so it must be the real
// production sync host. CloudSyncService issues relative "api/v1/..." requests
// against this base (see EggLedgerSyncServer Config.cs RedirectUrl host). Menno's
// data URL is absolute, so it is unaffected by this base.
// MANUAL-VERIFY: confirm this matches the deployed sync server origin.
appBuilder.Services.AddEggLedgerWeb(CloudSyncBaseAddress());

// D2 - native SQLite storage. Replaces the browser IndexedDB stores with a
// SQLite-backed IIndexedDb (so every store goes native) and switches the report
// path to the live SQL ReportExecutor. Data lives under the exe directory's data
// root (honoring bootstrap.json relocation). Must run after AddEggLedgerWeb so
// the overrides win.
var dataRootDir = StoragePaths.ResolveDataRootDir(AppContext.BaseDirectory);
appBuilder.Services.AddDesktopSqliteStorage(dataRootDir);

// D3 - native platform capabilities. Replaces the browser no-op IPlatformCapabilities
// with the native impl (open file, reveal in folder, native save dialog, restart,
// window size). The Photino window only exists after Build, so the window-backed
// seam is registered now as a late-bound holder and attached below. Must run after
// AddEggLedgerWeb so the override wins.
var desktopWindow = new PhotinoDesktopWindow();
appBuilder.Services.AddDesktopPlatformCapabilities(new ProcessRunner(), desktopWindow);

// D4 - live self-update status provider for the About overlay (active on desktop;
// the browser keeps the no-op). The running version is the EggLedger product
// version embedded as the assembly informational version (the Go build embeds
// VERSION); falls back to the file version. Must run after AddEggLedgerWeb so the
// override wins. The default exit action (Environment.Exit) is the real OLD-instance
// exit run after the new instance reports /ready, so it can rename EggLedger_new ->
// EggLedger (matches main.go exiting after HandoffChan).
var runningVersion = DesktopAppVersion();
appBuilder.Services.AddDesktopUpdater(() => runningVersion);

// D5 - desktop export sink. Replaces the browser download.js shim with a
// save-to-disk sink: export bytes are written to a path picked via the native
// save dialog (D3) and the file is revealed. Must run after AddEggLedgerWeb (to
// override) and after the platform caps registration (it depends on them).
appBuilder.Services.AddDesktopExportSink();

// The desktop host page is named desktop.html (not index.html) so it does not
// collide in the static-web-assets manifest with the referenced WASM project's
// own index.html. Point Photino's Blazor host at it.
appBuilder.Services.Configure<PhotinoBlazorAppConfiguration>(opts => opts.HostPage = "desktop.html");

appBuilder.RootComponents.Add<App>("#app");

var app = appBuilder.Build();

// Bind the now-built Photino window to the late-bound desktop window seam so
// GetWindowSize and the native save dialog operate on the real window.
desktopWindow.Attach(app.MainWindow);

// Window config ported from the Go/lorca setup (EggLedger/main.go: lorca.New with
// title "EggLedger", an app icon, and a width/height preference). The Go build read
// the size from stored user prefs (GetDefaultResolution); that preference store is
// D2, so for now we use a sensible default and center the window.
// MANUAL-VERIFY: launching this should open a native window showing the UI.
const int defaultWidth = 1280;
const int defaultHeight = 800;

app.MainWindow
    .SetTitle("EggLedger")
    .SetIconFile("icon-64.png")
    .SetUseOsDefaultSize(false)
    .SetSize(defaultWidth, defaultHeight)
    .SetUseOsDefaultLocation(false)
    .Center();

AppDomain.CurrentDomain.UnhandledException += (_, error) =>
{
    app.MainWindow.ShowMessage("Fatal exception", error.ExceptionObject.ToString() ?? "Unknown error");
};

app.Run();

// Production cloud-sync host. CloudSyncService issues relative "api/v1/..." paths
// against this base; the trailing slash is required so relative URIs resolve under
// the host root. Derived from EggLedgerSyncServer Config.cs (RedirectUrl host
// https://ledgersync.davidarthurcole.me).
static Uri CloudSyncBaseAddress() => new("https://ledgersync.davidarthurcole.me/");

// Resolve the EggLedger product version for the updater's compare. Prefer the
// assembly informational version (set from VERSION at build), strip any build
// metadata suffix (+sha), and fall back to a sane default.
static string DesktopAppVersion()
{
    var info = Assembly.GetEntryAssembly()?
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
    if (!string.IsNullOrEmpty(info))
    {
        var plus = info.IndexOf('+', StringComparison.Ordinal);
        return plus >= 0 ? info[..plus] : info;
    }
    return "2.1.4";
}
