using EggLedger.Domain.Api;
using EggLedger.Domain.MissionQuery;
using EggLedger.Domain.Reports;
using EggLedger.Web.Data;
using EggLedger.Web.Platform;
using EggLedger.Web.Services;
using EggLedger.Web.State;
using Microsoft.Extensions.DependencyInjection;

namespace EggLedger.Web;

/// <summary>
/// Shared DI registration for the EggLedger Blazor UI, used by both the browser
/// WASM entrypoint and the Photino desktop host so the service list stays in one
/// place. Host-specific bits (HttpClient base address, platform capabilities,
/// storage) are supplied by the caller / overridden afterwards.
/// </summary>
public static class WebServiceRegistration
{
    /// <summary>
    /// Register the host-agnostic UI services plus the browser defaults
    /// (IndexedDB stores, browser platform capabilities). A desktop host calls
    /// this then overrides storage (D2) and platform capabilities (D3).
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <param name="httpBaseAddress">Base address for the same-origin HttpClient.</param>
    public static IServiceCollection AddEggLedgerWeb(this IServiceCollection services, Uri httpBaseAddress)
    {
        services.AddScoped(_ => new HttpClient { BaseAddress = httpBaseAddress });

        // IndexedDB persistence. The browser owns one instance per app; the desktop
        // host (D2) swaps these for the native SQLite-backed stores.
        services.AddScoped<IIndexedDb, IndexedDb>();
        services.AddScoped<IndexedDbSettings>();
        services.AddScoped<IndexedDbAccountStore>();
        services.AddScoped<IndexedDbReportStore>();
        services.AddScoped<IndexedDbMissionStore>(sp => new IndexedDbMissionStore(
            sp.GetRequiredService<IIndexedDb>(),
            accounts: sp.GetRequiredService<IndexedDbAccountStore>()));
        services.AddScoped<IMissionStore>(sp => sp.GetRequiredService<IndexedDbMissionStore>());
        services.AddScoped<IndexedDbMissionDb>();
        services.AddScoped<IReportRunner>(sp => sp.GetRequiredService<IndexedDbMissionDb>());

        // Mission query handler layer over the store, with eiafx-backed quality.
        services.AddScoped<IArtifactQuality>(_ => EiafxQualityAdapter.Instance);
        services.AddScoped<MissionQueryHandlers>();

        // Shared, read-only mission filter configuration for the Mission Data tab.
        services.AddScoped<EggLedger.Web.Missions.MissionConfigProvider>();

        // Egg Inc API client over the same-origin HttpClient, and the fetch pipeline.
        services.AddScoped(sp => new ApiClient(sp.GetRequiredService<HttpClient>()));
        services.AddScoped<FetchService>();
        services.AddScoped<AddAccountService>();

        // Serves CSV/XLSX export bytes as a browser download via the download.js shim.
        // The UI binds IDownloadService so the desktop host can swap in a save-to-disk
        // sink (D5); the concrete impl is also resolvable for IAsyncDisposable cleanup.
        services.AddScoped<DownloadService>();
        services.AddScoped<IDownloadService>(sp => sp.GetRequiredService<DownloadService>());

        // Report weights backed by the canonical eiafx data; stateless shared instance.
        services.AddScoped<IWeightData>(_ => EiafxWeightData.Instance);

        // Menno community drop-rate stats client (read-only, in-memory cache).
        services.AddScoped<MennoService>();

        // Cloud sync: Discord OAuth (browser redirect + poll) + AES-encrypted blob
        // transport. The redirect is abstracted behind INavigation for testability.
        services.AddScoped<INavigation, BlazorNavigation>();
        services.AddScoped<CloudSyncService>();
        services.AddScoped<EggLedger.Web.Settings.CloudSessionStore>();

        // Shell state and the platform capability seam. Default is the browser
        // (non-desktop) impl; the desktop host overrides it with a native impl.
        services.AddScoped<ActiveAccount>();
        services.AddScoped<AppStateService>();
        services.AddScoped<AccountLoader>();
        services.AddScoped<IPlatformCapabilities, BrowserPlatformCapabilities>();

        // Update-status surface for the About overlay. Browser default is the
        // permanently-up-to-date no-op; the desktop host overrides with the live
        // updater-backed provider. The overlay is gated on IsDesktop regardless.
        services.AddScoped<IUpdateStatusProvider, NoOpUpdateStatusProvider>();

        return services;
    }
}
