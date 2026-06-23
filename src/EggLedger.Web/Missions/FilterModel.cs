namespace EggLedger.Web.Missions;

/// <summary>
/// One filter condition. C# port of the Vue <c>FilterCondition</c> in
/// useFilters.ts: a field key (<see cref="TopLevel"/>), an operator
/// (<see cref="Op"/>), and a string value (<see cref="Val"/>). All three are
/// strings to match the Vue model exactly; numeric and date comparisons parse
/// <see cref="Val"/> at match time.
/// </summary>
public sealed class FilterCondition
{
    public string TopLevel { get; set; } = "";
    public string Op { get; set; } = "";
    public string Val { get; set; } = "";

    public FilterCondition() { }

    public FilterCondition(string topLevel, string op, string val)
    {
        TopLevel = topLevel;
        Op = op;
        Val = val;
    }
}

/// <summary>
/// Value-picker kind for a filter field. C# port of the Vue
/// <c>FilterValueKind</c> in filterFields.ts.
/// </summary>
public enum FilterValueKind
{
    Select,
    Modal,
    Date,
    Bool,
    Number,
}

/// <summary>
/// A selectable filter value option. C# port of the Vue <c>FilterOption</c> in
/// filterOptions.ts. Optional presentation fields (<see cref="StyleClass"/>,
/// <see cref="ImagePath"/>, <see cref="Rarity"/>, <see cref="RarityGif"/>) are
/// consumed by the modal/select pickers.
/// </summary>
public sealed class FilterOption
{
    public string Text { get; set; } = "";
    public string Value { get; set; } = "";
    public string? StyleClass { get; set; }
    public string? ImagePath { get; set; }
    public int? Rarity { get; set; }
    public string? RarityGif { get; set; }
}

/// <summary>An operator choice (value + label). Port of Vue <c>FilterOp</c>.</summary>
public sealed record FilterOp(string Value, string Label);
