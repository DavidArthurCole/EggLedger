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
            sp.GetRequiredService<IApiPayloadDecoder>(),
            accounts: sp.GetRequiredService<IndexedDbAccountStore>()));
        services.AddScoped<IMissionStore>(sp => sp.GetRequiredService<IndexedDbMissionStore>());
        services.AddScoped<IndexedDbMissionDb>();
        services.AddScoped<IReportRunner>(sp => sp.GetRequiredService<IndexedDbMissionDb>());

        // Mission query handler layer over the store, with eiafx-backed quality.
        services.AddScoped<IArtifactQuality>(_ => EiafxQualityAdapter.Instance);
        services.AddScoped<MissionQueryHandlers>();

        // Shared, read-only mission filter configuration for the Mission Data tab.
        services.AddScoped<EggLedger.Web.Missions.MissionConfigProvider>();

        // Egg Inc API client. The browser cannot call the auxbrain host directly
        // (CORS blocks it), so the WASM build points at a SAME-ORIGIN path that the
        // host (nginx / the desktop) reverse-proxies to the real API. Endpoints are
        // appended to this prefix, so "/egg-api" + "/ei/bot_first_contact" resolves
        // against the HttpClient BaseAddress (the serving origin) -> proxied upstream.
        services.AddScoped(sp => new ApiClient(sp.GetRequiredService<HttpClient>(), apiPrefix: "/egg-api"));

        // Decode seam: default to in-process protobuf-net decode. The WASM host
        // overrides this with a server-delegating decoder (protobuf-net cannot emit
        // in the browser). Desktop keeps the local path.
        services.AddScoped<IApiPayloadDecoder>(sp => new LocalApiPayloadDecoder(sp.GetRequiredService<ApiClient>()));

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
        // Blob crypto seam: the browser cannot run managed AES-GCM (AesGcm is
        // unsupported on the WASM runtime), so the default is the SubtleCrypto
        // (JS interop) cipher. The desktop host overrides this with the managed
        // LocalBlobCipher.
        services.AddScoped<IBlobCipher, SubtleCryptoBlobCipher>();
        services.AddScoped<CloudSyncService>();
        services.AddScoped<EggLedger.Web.Settings.CloudSessionStore>();

        // Shell state and the platform capability seam. Default is the browser
        // (non-desktop) impl; the desktop host overrides it with a native impl.
        services.AddScoped<ActiveAccount>();
        services.AddScoped<ScreenshotSafetyState>();
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
