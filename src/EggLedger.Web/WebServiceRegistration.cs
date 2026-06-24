using EggLedger.Domain.Api;
using EggLedger.Domain.MissionQuery;
using EggLedger.Domain.Reports;
using EggLedger.Web.Data;
using EggLedger.Web.Platform;
using EggLedger.Web.Services;
using EggLedger.Web.State;
using Microsoft.Extensions.DependencyInjection;

namespace EggLedger.Web;

/// <summary>Shared DI registration for the Blazor UI, used by both WASM and the desktop host. Host-specific bits (HttpClient base, platform capabilities, storage) are supplied or overridden by the caller.</summary>
public static class WebServiceRegistration
{
    /// <summary>Register host-agnostic UI services plus browser defaults. A desktop host calls this then overrides storage (D2) and platform capabilities (D3).</summary>
    public static IServiceCollection AddEggLedgerWeb(this IServiceCollection services, Uri httpBaseAddress)
    {
        services.AddScoped(_ => new HttpClient { BaseAddress = httpBaseAddress });

        // IndexedDB persistence. The desktop host (D2) swaps these for native SQLite-backed stores.
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

        // Egg Inc API client. CORS blocks calling auxbrain directly, so the prefix is a
        // same-origin path the host reverse-proxies upstream (resolved against the HttpClient BaseAddress).
        services.AddScoped(sp => new ApiClient(sp.GetRequiredService<HttpClient>(), apiPrefix: "/egg-api"));

        // Decode seam: default in-process protobuf-net decode. WASM overrides with a
        // server-delegating decoder (protobuf-net cannot emit in the browser); desktop keeps the local path.
        services.AddScoped<IApiPayloadDecoder>(sp => new LocalApiPayloadDecoder(sp.GetRequiredService<ApiClient>()));

        services.AddScoped<FetchService>();
        services.AddScoped<AddAccountService>();

        // CSV/XLSX export download. UI binds IDownloadService so desktop can swap a save-to-disk
        // sink (D5); the concrete impl is also resolvable for IAsyncDisposable cleanup.
        services.AddScoped<DownloadService>();
        services.AddScoped<IDownloadService>(sp => sp.GetRequiredService<DownloadService>());

        // Report weights backed by the canonical eiafx data; stateless shared instance.
        services.AddScoped<IWeightData>(_ => EiafxWeightData.Instance);

        // Menno community drop-rate stats client (read-only, in-memory cache).
        services.AddScoped<MennoService>();

        // Cloud sync: Discord OAuth + AES-encrypted blobs. Redirect is behind INavigation for testability.
        services.AddScoped<INavigation, BlazorNavigation>();
        // Blob crypto seam: WASM has no managed AES-GCM, so default is the SubtleCrypto
        // (JS interop) cipher; desktop overrides with the managed LocalBlobCipher.
        services.AddScoped<IBlobCipher, SubtleCryptoBlobCipher>();
        services.AddScoped<CloudSyncService>();
        services.AddScoped<EggLedger.Web.Settings.CloudSessionStore>();

        // Shell state and the platform capability seam. Desktop overrides the browser impl with a native one.
        services.AddScoped<ActiveAccount>();
        services.AddScoped<ScreenshotSafetyState>();
        services.AddScoped<AppStateService>();
        services.AddScoped<AccountLoader>();
        services.AddScoped<IPlatformCapabilities, BrowserPlatformCapabilities>();

        // Update-status surface for the About overlay. Browser default is the no-op; desktop
        // overrides with the live updater. Overlay is gated on IsDesktop regardless.
        services.AddScoped<IUpdateStatusProvider, NoOpUpdateStatusProvider>();

        return services;
    }
}
