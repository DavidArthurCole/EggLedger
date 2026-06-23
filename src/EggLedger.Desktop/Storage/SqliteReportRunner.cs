using EggLedger.Domain.Reports;
using EggLedger.Web.Data;

namespace EggLedger.Desktop.Storage;

/// <summary>
/// Desktop <see cref="IReportRunner"/> that runs reports through the live SQL
/// path: <see cref="ReportExecutor"/> over <see cref="SqliteMissionDb"/>, exactly
/// as the Go host ran them against its injected <c>*sql.DB</c>. The browser uses
/// <see cref="IndexedDbMissionDb"/> (in-memory runner over materialized rows);
/// both produce identical <see cref="ReportResult"/> output (parity-tested).
/// </summary>
public sealed class SqliteReportRunner : IReportRunner
{
    private readonly SqliteMissionDb _db;
    private readonly IWeightData _weights;

    public SqliteReportRunner(SqliteMissionDb db, IWeightData weights)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _weights = weights ?? throw new ArgumentNullException(nameof(weights));
    }

    public Task<ReportResult> RunReportAsync(ReportDefinition def, string accountId)
    {
        // ExecuteReport binds m.player_id = ? from def.AccountId. The browser runner
        // materializes rows by the passed accountId but its in-memory db filters on
        // def.AccountId, so the two agree only when they match. Bind the caller's
        // accountId explicitly to keep the SQL path consistent with that contract.
        if (def.AccountId != accountId)
        {
            def.AccountId = accountId;
        }
        var executor = new ReportExecutor(_db, _weights);
        return Task.FromResult(executor.ExecuteReport(def));
    }
}
