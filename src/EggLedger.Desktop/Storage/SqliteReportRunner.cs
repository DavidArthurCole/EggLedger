using EggLedger.Domain.Reports;
using EggLedger.Web.Data;

namespace EggLedger.Desktop.Storage;

/// <summary>
/// Desktop <see cref="IReportRunner"/> running the live SQL path: ReportExecutor
/// over SqliteMissionDb. The browser uses the in-memory IndexedDbMissionDb runner;
/// both produce identical ReportResult output (parity-tested).
/// </summary>
public sealed class SqliteReportRunner : IReportRunner {
    private readonly SqliteMissionDb _db;
    private readonly IWeightData _weights;

    public SqliteReportRunner(SqliteMissionDb db, IWeightData weights) {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _weights = weights ?? throw new ArgumentNullException(nameof(weights));
    }

    public Task<ReportResult> RunReportAsync(ReportDefinition def, string accountId) {
        // ExecuteReport binds m.player_id = ? from def.AccountId. Force it to the
        // caller's accountId so the SQL path matches the browser runner's contract.
        if (def.AccountId != accountId) {
            def.AccountId = accountId;
        }
        var executor = new ReportExecutor(_db, _weights);
        return Task.FromResult(executor.ExecuteReport(def));
    }
}
