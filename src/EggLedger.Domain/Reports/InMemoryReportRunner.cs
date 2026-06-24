using System.Globalization;

namespace EggLedger.Domain.Reports;

/// <summary>
/// A single mission row, typed equivalent of the SQLite <c>mission</c> columns the
/// report queries read. Browser materializes these from IndexedDB.
/// </summary>
public sealed record MissionRowData
{
    public string PlayerId { get; init; } = "";
    public string MissionId { get; init; } = "";
    public long Ship { get; init; }
    public long DurationType { get; init; }
    public long Level { get; init; }
    public long Target { get; init; }
    public long MissionType { get; init; }
    public long StartTimestamp { get; init; }
    public long ReturnTimestamp { get; init; }
    public long Capacity { get; init; }
    public long NominalCapacity { get; init; }
    public bool IsDubCap { get; init; }
    public bool IsBuggedCap { get; init; }
}

/// <summary>A single artifact-drop row, typed equivalent of the SQLite <c>artifact_drops</c> columns.</summary>
public sealed record ArtifactDropRowData
{
    public string PlayerId { get; init; } = "";
    public string MissionId { get; init; } = "";
    public long DropIndex { get; init; }
    public long ArtifactId { get; init; }
    public string SpecType { get; init; } = "";
    public long Level { get; init; }
    public long Rarity { get; init; }
    public double Quality { get; init; }
}

/// <summary>
/// SQL-free report execution path for the browser (IndexedDB has no SQL engine).
/// Reuses <see cref="ReportExecutor"/> verbatim by wrapping the typed rows in an
/// in-memory <see cref="IMissionDb"/>, so the report math is identical by construction.
/// </summary>
public sealed class InMemoryReportRunner
{
    private readonly IWeightData _weights;

    public InMemoryReportRunner(IWeightData weights)
    {
        _weights = weights ?? throw new ArgumentNullException(nameof(weights));
    }

    /// <summary>
    /// Executes the report against the supplied rows. Filters by <c>def.AccountId</c>
    /// internally, so callers may pass all rows or only the player's rows.
    /// </summary>
    public ReportResult Run(
        ReportDefinition def,
        IReadOnlyList<MissionRowData> missions,
        IReadOnlyList<ArtifactDropRowData> drops)
    {
        var db = new InMemoryMissionDb(def, missions, drops, _weights);
        var executor = new ReportExecutor(db, _weights);
        return executor.ExecuteReport(def);
    }
}
