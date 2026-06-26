namespace EggLedger.Domain.Reports;

/// <summary>Whether a dimension groups missions or artifact drops. Port of the Vue ReportDimension.scope union.</summary>
public enum DimensionScope {
    Mission,
    Artifact,
}

/// <summary>
/// Report group-by dimension metadata. Port of Vue utils/reportDimensions.ts.
/// <see cref="Value"/> strings are wire/SQL keys and must never change; labels are display-only.
/// </summary>
public sealed record ReportDimension(string Value, string Label, DimensionScope Scope);

/// <summary>Static dimension tables and lookups for the report builders. Port of Vue reportDimensions.ts.</summary>
public static class ReportDimensions {
    public static readonly IReadOnlyList<ReportDimension> Mission = new[]
    {
        new ReportDimension("ship_type", "Ship Type", DimensionScope.Mission),
        new ReportDimension("duration_type", "Duration Type", DimensionScope.Mission),
        new ReportDimension("level", "Level", DimensionScope.Mission),
        new ReportDimension("mission_type", "Mission Type", DimensionScope.Mission),
        new ReportDimension("mission_target", "Mission Target", DimensionScope.Mission),
    };

    public static readonly IReadOnlyList<ReportDimension> Artifact = new[]
    {
        new ReportDimension("artifact_name", "Artifact Name", DimensionScope.Artifact),
        new ReportDimension("rarity", "Rarity", DimensionScope.Artifact),
        new ReportDimension("tier", "Tier", DimensionScope.Artifact),
        new ReportDimension("spec_type", "Spec Type", DimensionScope.Artifact),
    };

    /// <summary>Mission dimensions first.</summary>
    public static readonly IReadOnlyList<ReportDimension> All =
        Mission.Concat(Artifact).ToList();

    public static readonly IReadOnlySet<string> ArtifactKeys =
        Artifact.Select(d => d.Value).ToHashSet(StringComparer.Ordinal);

    /// <summary>Title Case label for a dimension value, falling back to the value. Port of Vue dimensionLabel.</summary>
    public static string DimensionLabel(string value) =>
        All.FirstOrDefault(d => d.Value == value)?.Label ?? value;
}
