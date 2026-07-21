namespace EggLedger.Web.Missions;

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

public enum FilterValueKind {
    Select,
    Modal,
    Date,
    Bool,
    Number,
}

public sealed class FilterOption {
    public string Text { get; set; } = "";
    public string Value { get; set; } = "";
    public string? StyleClass { get; set; }
    public string? ImagePath { get; set; }
    public int? Rarity { get; set; }
    public string? GroupKey { get; set; }
    public string? GroupLabel { get; set; }
    public string? Badge { get; set; }
}

public sealed record FilterOp(string Value, string Label);
