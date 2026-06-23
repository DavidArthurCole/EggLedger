using EggLedger.Domain.Reports;

namespace EggLedger.Web.Data;

/// <summary>
/// Runs a report definition for an account and returns the labeled result. The
/// browser fills this with <see cref="IndexedDbMissionDb"/> (materializes rows
/// from IndexedDB into the in-memory report runner); the desktop host fills it
/// with a SQLite-backed runner that executes the SQL <see cref="ReportExecutor"/>
/// path natively. Same UI, two backends.
/// </summary>
public interface IReportRunner
{
    /// <summary>Runs <paramref name="def"/> for <paramref name="accountId"/>.</summary>
    Task<ReportResult> RunReportAsync(ReportDefinition def, string accountId);
}
