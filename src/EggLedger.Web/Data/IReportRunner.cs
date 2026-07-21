using EggLedger.Domain.Reports;

namespace EggLedger.Web.Data;

public interface IReportRunner {
    Task<ReportResult> RunReportAsync(ReportDefinition def, string accountId);
}
