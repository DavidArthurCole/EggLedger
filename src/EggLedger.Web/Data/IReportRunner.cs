using EggLedger.Domain.Reports;

namespace EggLedger.Web.Data;

/// <summary>
/// Runs a report definition for an account. Browser uses <see cref="IndexedDbMissionDb"/>
/// (in-memory runner); desktop uses a SQLite-backed <see cref="ReportExecutor"/> runner.
/// </summary>
public interface IReportRunner
{
    /// <summary>Runs <paramref name="def"/> for <paramref name="accountId"/>.</summary>
    Task<ReportResult> RunReportAsync(ReportDefinition def, string accountId);
}
