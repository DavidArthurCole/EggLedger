using EggLedger.Domain.Api;
using EggLedger.Domain.MissionPacking;
using EggLedger.Domain.MissionQuery;
using EggLedger.Domain.Reports;
using EggLedger.Web.Data;
using EggLedger.Web.Platform;
using EggLedger.Web.Services;
using EggLedger.Web.State;
using Microsoft.Extensions.DependencyInjection;

namespace EggLedger.Web;

/// <summary>Shared DI registration for the Blazor UI. Host-specific bits (HttpClient base, platform capabilities, storage) are supplied or overridden by the caller.</summary>
public static class WebServiceRegistration {
    /// <summary>Registers host-agnostic UI services plus browser defaults. Desktop overrides storage and platform capabilities.</summary>
    public static IServiceCollection AddEggLedgerWeb(this IServiceCollection services, Uri httpBaseAddress) {
        services.AddScoped(_ => new HttpClient { BaseAddress = httpBaseAddress });

        // Desktop swaps these for native SQLite-backed stores.
        services.AddScoped<IIndexedDb, IndexedDb>();
        services.AddScoped<IndexedDbSettings>();
        services.AddScoped<IndexedDbAccountStore>();
        services.AddScoped<IndexedDbReportStore>();
        services.AddScoped<IndexedDbMissionStore>(sp => new IndexedDbMissionStore(
            sp.GetRequiredService<IIndexedDb>(),
            sp.GetRequiredService<IApiPayloadDecoder>(),
            accounts: sp.GetRequiredService<IndexedDbAccountStore>()));
        services.AddScoped<IMissionStore>(sp => sp.GetRequiredService<IndexedDbMissionStore>());
        services.AddScoped<IndexedDbReportRunner>();
        services.AddScoped<IReportRunner>(sp => sp.GetRequiredService<IndexedDbReportRunner>());

        services.AddSingleton<IArtifactQuality>(_ => EiafxQualityAdapter.Instance);
        services.AddScoped<IMissionCompiler>(_ => new MissionPacker(EiafxMissionConfigSource.Instance));
        services.AddScoped<MissionQueryHandlers>();

        services.AddSingleton<EggLedger.Web.Missions.MissionConfigProvider>();

        // CORS blocks auxbrain directly; the prefix is a same-origin path the host reverse-proxies upstream.
        services.AddScoped(sp => new ApiClient(sp.GetRequiredService<HttpClient>(), apiPrefix: "/egg-api"));

        services.AddScoped<IApiPayloadDecoder>(sp => new LocalApiPayloadDecoder(sp.GetRequiredService<ApiClient>()));

        services.AddScoped<FetchService>();
        services.AddScoped<AddAccountService>();

        // UI binds IDownloadService so desktop can swap a save-to-disk sink; the concrete impl stays resolvable for IAsyncDisposable cleanup.
        services.AddScoped<DownloadService>();
        services.AddScoped<IDownloadService>(sp => sp.GetRequiredService<DownloadService>());

        services.AddSingleton<IWeightData>(_ => EiafxWeightData.Instance);

        // Singleton: the multi-MB community payload downloads once for all circuits. Owns a
        // dedicated long-lived HttpClient (not the scoped one) to avoid a captive dependency.
        services.AddSingleton(_ => new MennoService(new HttpClient()));

        // Redirect is behind INavigation for testability.
        services.AddScoped<INavigation, BlazorNavigation>();
        services.AddScoped<IBlobCipher, LocalBlobCipher>();
        services.AddScoped<CloudSyncService>();
        services.AddScoped<AdminService>();
        services.AddScoped<AdminState>();
        services.AddScoped<EggLedger.Web.Settings.CloudSessionStore>();

        // Desktop overrides the browser platform impl with a native one.
        services.AddScoped<ActiveAccount>();
        services.AddScoped<ScreenshotSafetyState>();
        services.AddScoped<AppStateService>();
        services.AddScoped<AccountLoader>();
        services.AddScoped<IPlatformCapabilities, BrowserPlatformCapabilities>();

        // Desktop storage/export seams: browser no-ops; desktop swaps native impls.
        services.AddScoped<BrowserStorageManagement>();
        services.AddScoped<IStorageManagement>(sp => sp.GetRequiredService<BrowserStorageManagement>());
        services.AddScoped<IExportManagement>(sp => sp.GetRequiredService<BrowserStorageManagement>());

        // Browser default is the no-op; desktop overrides with the live updater.
        services.AddScoped<IUpdateStatusProvider, NoOpUpdateStatusProvider>();

        return services;
    }
}
