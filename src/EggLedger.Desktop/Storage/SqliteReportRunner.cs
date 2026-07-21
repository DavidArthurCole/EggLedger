using EggLedger.Domain.Reports;
using EggLedger.Web.Data;

namespace EggLedger.Desktop.Storage;

public sealed class SqliteReportRunner : IReportRunner {
    private readonly SqliteMissionDb _db;
    private readonly IWeightData _weights;

    public SqliteReportRunner(SqliteMissionDb db, IWeightData weights) {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _weights = weights ?? throw new ArgumentNullException(nameof(weights));
    }

    public Task<ReportResult> RunReportAsync(ReportDefinition def, string accountId) {
        
        
        if (def.AccountId != accountId) {
            def.AccountId = accountId;
        }
        var executor = new ReportExecutor(_db, _weights);
        return Task.FromResult(executor.ExecuteReport(def));
    }
}
