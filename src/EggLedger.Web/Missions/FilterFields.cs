using EggLedger.Domain.MissionQuery;

namespace EggLedger.Web.Missions;

public sealed class FilterFieldCtx {
    public IReadOnlyList<PossibleTarget> PossibleTargets { get; init; } = Array.Empty<PossibleTarget>();
    public IReadOnlyList<PossibleArtifact> ArtifactConfigs { get; init; } = Array.Empty<PossibleArtifact>();
    public double MaxQuality { get; init; }
}

public sealed class FilterFieldDef {
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";

    /// <summary>"mission" or "artifact".</summary>
    public string Scope { get; init; } = "mission";
    public FilterValueKind ValueKind { get; init; }
    public IReadOnlyList<FilterOp> Ops { get; init; } = Array.Empty<FilterOp>();

    /// <summary>For select/modal kinds: builds the option list. Null otherwise.</summary>
    public Func<FilterFieldCtx, List<FilterOption>>? OptionsSource { get; init; }
}

/// <summary>Static filter field/operator definitions, golden-matched to filterFields.ts (field set, operator lists, default-operator rules, scope partitions).</summary>
public static class FilterFields {
    public static readonly IReadOnlyList<FilterOp> EqualityOps = new[]
    {
        new FilterOp("=", "is"),
        new FilterOp("!=", "is not"),
    };

    public static readonly IReadOnlyList<FilterOp> ComparisonOps = new[]
    {
        new FilterOp("=", "is"),
        new FilterOp("!=", "is not"),
        new FilterOp(">", "greater than"),
        new FilterOp("<", "less than"),
        new FilterOp(">=", "at least"),
        new FilterOp("<=", "at most"),
    };

    public static readonly IReadOnlyList<FilterOp> DateOps = new[]
    {
        new FilterOp("=", "on"),
        new FilterOp("<", "before"),
        new FilterOp(">", "after"),
        new FilterOp("<=", "on or before"),
        new FilterOp(">=", "on or after"),
    };

    /// <summary>Date operators for the Mission Data tab filter bar. "on" must map to "d=" (day-equality), since a date "=" reference-compares and never matches (see MissionFilterMatcher).</summary>
    public static readonly IReadOnlyList<FilterOp> MissionDateOps = new[]
    {
        new FilterOp("d=", "on"),
        new FilterOp("<", "before"),
        new FilterOp(">", "after"),
    };

    /// <summary>Operators the Mission Data filter bar presents for a field. Date fields use MissionDateOps; everything else uses the field's own Ops.</summary>
    public static IReadOnlyList<FilterOp> MissionBarOpsFor(FilterFieldDef def) =>
        def.ValueKind == FilterValueKind.Date ? MissionDateOps : def.Ops;

    public static readonly IReadOnlyList<FilterOp> DropsOps = new[]
    {
        new FilterOp("c", "contains"),
        new FilterOp("dnc", "does not contain"),
    };

    public static readonly IReadOnlyList<FilterOp> BoolOps = new[]
    {
        new FilterOp("true", "True"),
        new FilterOp("false", "False"),
    };

    /// <summary>All filter fields, mission then artifact scope.</summary>
    public static readonly IReadOnlyList<FilterFieldDef> ReportFilterFields = new[]
    {
        new FilterFieldDef
        {
            Key = "ship", Label = "Ship", Scope = "mission", ValueKind = FilterValueKind.Select,
            Ops = ComparisonOps, OptionsSource = _ => FilterOptions.GetMissionFilterValueOptions("ship"),
        },
        new FilterFieldDef
        {
            Key = "duration", Label = "Duration", Scope = "mission", ValueKind = FilterValueKind.Select,
            Ops = ComparisonOps, OptionsSource = _ => FilterOptions.GetMissionFilterValueOptions("duration"),
        },
        new FilterFieldDef
        {
            Key = "level", Label = "Level", Scope = "mission", ValueKind = FilterValueKind.Select,
            Ops = ComparisonOps, OptionsSource = _ => FilterOptions.GetMissionFilterValueOptions("level"),
        },
        new FilterFieldDef
        {
            Key = "target", Label = "Target", Scope = "mission", ValueKind = FilterValueKind.Modal,
            Ops = EqualityOps, OptionsSource = ctx => FilterOptions.GetTargetFilterOptions(ctx.PossibleTargets),
        },
        new FilterFieldDef
        {
            Key = "type", Label = "Mission Type", Scope = "mission", ValueKind = FilterValueKind.Select,
            Ops = EqualityOps, OptionsSource = _ => FilterOptions.GetMissionFilterValueOptions("type"),
        },
        new FilterFieldDef
        {
            Key = "launchDT", Label = "Launch Date", Scope = "mission", ValueKind = FilterValueKind.Date,
            Ops = DateOps,
        },
        new FilterFieldDef
        {
            Key = "returnDT", Label = "Return Date", Scope = "mission", ValueKind = FilterValueKind.Date,
            Ops = DateOps,
        },
        new FilterFieldDef
        {
            Key = "dubcap", Label = "Dub cap", Scope = "mission", ValueKind = FilterValueKind.Bool,
            Ops = BoolOps,
        },
        new FilterFieldDef
        {
            Key = "buggedcap", Label = "Bugged cap", Scope = "mission", ValueKind = FilterValueKind.Bool,
            Ops = BoolOps,
        },
        new FilterFieldDef
        {
            Key = "drops", Label = "Drops", Scope = "mission", ValueKind = FilterValueKind.Modal,
            Ops = DropsOps,
            OptionsSource = ctx => FilterOptions.GetDropFilterOptions(ctx.ArtifactConfigs, ctx.MaxQuality, true),
        },
        new FilterFieldDef
        {
            Key = "artifact_name", Label = "Name", Scope = "artifact", ValueKind = FilterValueKind.Modal,
            Ops = EqualityOps, OptionsSource = ctx => FilterOptions.GetArtifactNameFilterOptions(ctx.ArtifactConfigs),
        },
        new FilterFieldDef
        {
            Key = "artifact_rarity", Label = "Rarity", Scope = "artifact", ValueKind = FilterValueKind.Select,
            Ops = ComparisonOps, OptionsSource = _ => FilterOptions.GetArtifactRarityFilterOptions(),
        },
        new FilterFieldDef
        {
            Key = "artifact_tier", Label = "Tier", Scope = "artifact", ValueKind = FilterValueKind.Select,
            Ops = ComparisonOps, OptionsSource = ctx => FilterOptions.GetArtifactTierFilterOptions(ctx.ArtifactConfigs),
        },
        new FilterFieldDef
        {
            Key = "artifact_spec_type", Label = "Spec Type", Scope = "artifact", ValueKind = FilterValueKind.Select,
            Ops = EqualityOps, OptionsSource = _ => FilterOptions.GetArtifactSpecTypeFilterOptions(),
        },
        new FilterFieldDef
        {
            Key = "artifact_quality", Label = "Quality", Scope = "artifact", ValueKind = FilterValueKind.Number,
            Ops = ComparisonOps,
        },
    };

    /// <summary>Field definition for a key, or null.</summary>
    public static FilterFieldDef? GetReportField(string key) {
        foreach (var f in ReportFilterFields) {
            if (f.Key == key) {
                return f;
            }
        }
        return null;
    }

    /// <summary>Default operator for a field in the Mission Data bar: bool -> "true", drops -> "c", date -> "d=", else first operator.</summary>
    public static string DefaultOpForField(FilterFieldDef def) {
        if (def.ValueKind == FilterValueKind.Bool) {
            return "true";
        }
        if (def.Key == "drops") {
            return "c";
        }
        var ops = MissionBarOpsFor(def);
        return ops.Count > 0 ? ops[0].Value : "";
    }

    /// <summary>Mission-scoped fields.</summary>
    public static List<FilterFieldDef> ReportMissionFields() {
        var result = new List<FilterFieldDef>();
        foreach (var f in ReportFilterFields) {
            if (f.Scope == "mission") {
                result.Add(f);
            }
        }
        return result;
    }

    /// <summary>Artifact-scoped fields.</summary>
    public static List<FilterFieldDef> ReportArtifactFields() {
        var result = new List<FilterFieldDef>();
        foreach (var f in ReportFilterFields) {
            if (f.Scope == "artifact") {
                result.Add(f);
            }
        }
        return result;
    }
}
