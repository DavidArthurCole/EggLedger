namespace EggLedger.Web.Components.Reports;

/// <summary>A filter operator: wire value plus display label.</summary>
public readonly record struct FilterOp(string Value, string Label);

/// <summary>A report filter field: key, label, scope, and operator set. Option lists are left to the UI.</summary>
public sealed record ReportFilterField(string Key, string Label, string Scope, IReadOnlyList<FilterOp> Ops);

/// <summary>Static report filter field table.</summary>
public static class ReportFilterFields {
    private static readonly IReadOnlyList<FilterOp> EqualityOps = new[]
    {
        new FilterOp("=", "is"),
        new FilterOp("!=", "is not"),
    };

    private static readonly IReadOnlyList<FilterOp> ComparisonOps = new[]
    {
        new FilterOp("=", "is"),
        new FilterOp("!=", "is not"),
        new FilterOp(">", "greater than"),
        new FilterOp("<", "less than"),
        new FilterOp(">=", "at least"),
        new FilterOp("<=", "at most"),
    };

    private static readonly IReadOnlyList<FilterOp> DateOps = new[]
    {
        new FilterOp("=", "on"),
        new FilterOp("<", "before"),
        new FilterOp(">", "after"),
        new FilterOp("<=", "on or before"),
        new FilterOp(">=", "on or after"),
    };

    /// <summary>All report filter fields, mission scope first.</summary>
    public static readonly IReadOnlyList<ReportFilterField> All = new[]
    {
        new ReportFilterField("ship", "Ship", "mission", ComparisonOps),
        new ReportFilterField("duration", "Duration", "mission", ComparisonOps),
        new ReportFilterField("level", "Level", "mission", ComparisonOps),
        new ReportFilterField("target", "Target", "mission", EqualityOps),
        new ReportFilterField("type", "Mission Type", "mission", EqualityOps),
        new ReportFilterField("launchDT", "Launch Date", "mission", DateOps),
        new ReportFilterField("returnDT", "Return Date", "mission", DateOps),
        new ReportFilterField("artifact_name", "Name", "artifact", EqualityOps),
        new ReportFilterField("artifact_rarity", "Rarity", "artifact", ComparisonOps),
        new ReportFilterField("artifact_tier", "Tier", "artifact", ComparisonOps),
        new ReportFilterField("artifact_spec_type", "Spec Type", "artifact", EqualityOps),
        new ReportFilterField("artifact_quality", "Quality", "artifact", ComparisonOps),
    };

    /// <summary>Returns the field for a key, or null.</summary>
    public static ReportFilterField? Get(string key) =>
        All.FirstOrDefault(f => f.Key == key);

    /// <summary>Default operator for a field (its first op).</summary>
    public static string DefaultOp(ReportFilterField f) =>
        f.Ops.Count > 0 ? f.Ops[0].Value : "";
}
