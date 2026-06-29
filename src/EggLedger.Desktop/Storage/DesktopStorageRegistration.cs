using EggLedger.Desktop.Export;
using EggLedger.Domain.MissionQuery;
using EggLedger.Domain.Reports;
using EggLedger.Web.Data;
using EggLedger.Web.Platform;
using EggLedger.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EggLedger.Desktop.Storage;

/// <summary>Registers the native SQLite storage backend behind the browser IndexedDB interfaces.</summary>
/// <remarks>
/// Call AFTER AddEggLedgerWeb so overrides win. Swaps IIndexedDb and IReportRunner;
/// other stores are unchanged because they funnel through IIndexedDb.
/// </remarks>
public static class DesktopStorageRegistration {
    /// <summary>
    /// Opens the mission and report SQLite DBs under <paramref name="dataRootDir"/>
    /// (migrating to v9 / v12) and registers them as singletons for the app lifetime.
    /// </summary>
    public static IServiceCollection AddDesktopSqliteStorage(this IServiceCollection services, string dataRootDir) {
        var internalDir = StoragePaths.ResolveInternalDir(dataRootDir);
        var missionDbPath = Path.Combine(internalDir, "ledger.db");
        var reportDbPath = Path.Combine(internalDir, "reports.db");

        var missionDb = SqliteDatabase.OpenMissionDb(missionDbPath);
        var reportDb = SqliteDatabase.OpenReportDb(reportDbPath);

        AddDesktopSqliteStorage(services, missionDb, reportDb);

        // Settings-tab storage + export-management services, keyed to the same data root.
        // Override the browser no-op seams (registered by AddEggLedgerWeb) with the native impls.
        services.AddScoped(sp => new DesktopStorageService(
            dataRootDir, sp.GetRequiredService<IPlatformCapabilities>()));
        services.RemoveAll<IStorageManagement>();
        services.AddScoped<IStorageManagement>(sp => sp.GetRequiredService<DesktopStorageService>());

        services.AddScoped(sp => {
            var accounts = sp.GetRequiredService<IndexedDbAccountStore>();
            return new DesktopExportService(
                dataRootDir,
                accountLookup: async () => (IReadOnlyList<AccountInfo>)await accounts.GetKnownAccountsAsync());
        });
        services.RemoveAll<IExportManagement>();
        services.AddScoped<IExportManagement>(sp => sp.GetRequiredService<DesktopExportService>());
        return services;
    }

    /// <summary>
    /// Registers SQLite services over already-open databases. Used by the path
    /// overload above and directly by tests that hold in-memory DBs.
    /// </summary>
    public static IServiceCollection AddDesktopSqliteStorage(
        this IServiceCollection services, SqliteDatabase missionDb, SqliteDatabase reportDb) {
        services.AddSingleton(missionDb);
        services.AddSingleton(reportDb);

        var indexedDb = new SqliteIndexedDb(missionDb, reportDb);
        // Replace the scoped browser IIndexedDb with the native singleton. RemoveAll
        // drops the IndexedDb registration so the SQLite one is resolved everywhere.
        services.RemoveAll<IIndexedDb>();
        services.AddSingleton<IIndexedDb>(indexedDb);

        services.AddSingleton(new SqliteMissionDb(missionDb));

        // The live SQL report path: ReportExecutor over real SQLite. Overrides the
        // browser in-memory runner; also drop the concrete browser report-runner so
        // it can't be resolved on desktop.
        services.RemoveAll<IndexedDbReportRunner>();
        services.RemoveAll<IReportRunner>();
        services.AddScoped<IReportRunner>(sp => new SqliteReportRunner(
            sp.GetRequiredService<SqliteMissionDb>(),
            sp.GetRequiredService<IWeightData>()));

        return services;
    }
}
