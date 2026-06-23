using EggLedger.Domain.Reports;
using EggLedger.Web.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EggLedger.Desktop.Storage;

/// <summary>
/// Registers the native SQLite storage backend for the desktop host, replacing
/// the browser IndexedDB services with SQLite-backed ones behind the same
/// interfaces. Call AFTER AddEggLedgerWeb so these overrides win.
///
/// Swaps:
///   IIndexedDb  -> SqliteIndexedDb  (makes every store SQLite-backed)
///   IReportRunner -> SqliteReportRunner (LIVE SQL ReportExecutor path)
/// Everything else (IMissionStore, report store, settings, accounts) is unchanged
/// because those concrete stores funnel through IIndexedDb.
/// </summary>
public static class DesktopStorageRegistration
{
    /// <summary>
    /// Opens the mission and report SQLite DBs under <paramref name="dataRootDir"/>
    /// (migrating to v9 / v12) and registers the SQLite services as singletons so
    /// the held connections live for the app lifetime.
    /// </summary>
    public static IServiceCollection AddDesktopSqliteStorage(this IServiceCollection services, string dataRootDir)
    {
        var internalDir = StoragePaths.ResolveInternalDir(dataRootDir);
        var missionDbPath = Path.Combine(internalDir, "ledger.db");
        var reportDbPath = Path.Combine(internalDir, "reports.db");

        var missionDb = SqliteDatabase.OpenMissionDb(missionDbPath);
        var reportDb = SqliteDatabase.OpenReportDb(reportDbPath);

        AddDesktopSqliteStorage(services, missionDb, reportDb);
        return services;
    }

    /// <summary>
    /// Registers SQLite services over already-open databases. Used by the path
    /// overload above and directly by tests that hold in-memory DBs.
    /// </summary>
    public static IServiceCollection AddDesktopSqliteStorage(
        this IServiceCollection services, SqliteDatabase missionDb, SqliteDatabase reportDb)
    {
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
        services.RemoveAll<IndexedDbMissionDb>();
        services.RemoveAll<IReportRunner>();
        services.AddScoped<IReportRunner>(sp => new SqliteReportRunner(
            sp.GetRequiredService<SqliteMissionDb>(),
            sp.GetRequiredService<IWeightData>()));

        return services;
    }
}
