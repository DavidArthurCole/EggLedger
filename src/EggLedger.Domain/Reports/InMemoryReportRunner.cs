using System.Globalization;

namespace EggLedger.Domain.Reports;

/// <summary>
/// A single mission row, the typed equivalent of the SQLite <c>mission</c> table
/// columns the report queries read. The browser materializes these from
/// IndexedDB; the SQL path reads the same columns from SQLite.
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

/// <summary>
/// A single artifact-drop row, the typed equivalent of the SQLite
/// <c>artifact_drops</c> table columns the report queries read.
/// </summary>
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
/// SQL-free report execution path for the browser. IndexedDB has no SQL engine,
/// so this runner takes the SAME ReportDefinition and the SAME materialized row
/// set the SQL path consumes and produces the SAME ReportResult.
///
/// It does so by reusing the validated math in <see cref="ReportExecutor"/>
/// verbatim: it builds an in-memory <see cref="IMissionDb"/> that answers every
/// query ReportExecutor issues by evaluating the filter/group semantics of
/// reports/query.go directly over the typed rows, then hands that DB to a real
/// ReportExecutor. The weight/label/classify/normalize/pivot/time-fill logic is
/// therefore identical by construction, not re-derived.
/// </summary>
public sealed class InMemoryReportRunner
{
    private readonly IWeightData _weights;

    public InMemoryReportRunner(IWeightData weights)
    {
        _weights = weights ?? throw new ArgumentNullException(nameof(weights));
    }

    /// <summary>
    /// Executes the report against the supplied rows. The runner filters by
    /// <c>def.AccountId</c> internally (the SQL path's <c>m.player_id = ?</c>), so
    /// callers may pass all rows or only the player's rows.
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
