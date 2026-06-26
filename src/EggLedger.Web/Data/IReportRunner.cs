using EggLedger.Domain.Reports;

namespace EggLedger.Web.Data;

/// <summary>
/// Runs a report definition for an account. Browser uses <see cref="IndexedDbReportRunner"/>
/// (in-memory runner); desktop uses a SQLite-backed <see cref="ReportExecutor"/> runner.
/// </summary>
public interface IReportRunner {
    Task<ReportResult> RunReportAsync(ReportDefinition def, string accountId);
}
