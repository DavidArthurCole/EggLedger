namespace EggLedger.Web.Missions;

/// <summary>One filter condition: field key, operator, string value. All strings to match the Vue model; numeric/date comparisons parse Val at match time.</summary>
public sealed class FilterCondition {
    public string TopLevel { get; set; } = "";
    public string Op { get; set; } = "";
    public string Val { get; set; } = "";
    public FilterCondition() { }

    public FilterCondition(string topLevel, string op, string val) {
        TopLevel = topLevel;
        Op = op;
        Val = val;
    }
}

/// <summary>Value-picker kind for a filter field.</summary>
public enum FilterValueKind {
    Select,
    Modal,
    Date,
    Bool,
    Number,
}

/// <summary>A selectable filter value option. Optional presentation fields are consumed by the modal/select pickers.</summary>
public sealed class FilterOption {
    public string Text { get; set; } = "";
    public string Value { get; set; } = "";
    public string? StyleClass { get; set; }
    public string? ImagePath { get; set; }
    public int? Rarity { get; set; }
    public string? RarityGif { get; set; }
}

/// <summary>An operator choice (value + label).</summary>
public sealed record FilterOp(string Value, string Label);
