namespace EggLedger.Web.Missions.Model;

/// <summary>Strongly-typed mission filter: OR of groups, AND within a group. The legacy {and, or} JSON survives only at the persistence boundary via FilterCodec.</summary>
public sealed record MissionFilter(IReadOnlyList<FilterGroup> Groups)
{
    public static readonly MissionFilter Empty = new(Array.Empty<FilterGroup>());

    /// <summary>True when there is nothing to filter (every mission passes).</summary>
    public bool IsEmpty => Groups.Count == 0 || Groups.All(g => g.Conditions.Count == 0);
}

/// <summary>One AND-group: matches when every condition matches.</summary>
public sealed record FilterGroup(IReadOnlyList<Condition> Conditions);

/// <summary>A single condition: a field, an operator, and a typed value.</summary>
public sealed record Condition(FilterField Field, FilterOperator Operator, FilterValue Value);

/// <summary>The mission attribute a condition tests.</summary>
public enum FilterField
{
    Ship,
    DurationType,
    Level,
    Capacity,
    Target,
    MissionType,
    LaunchDate,
    ReturnDate,
    DubCap,
    BuggedCap,
    Drops,
}

/// <summary>The value shape a field carries.</summary>
public enum FilterValueType
{
    Enum,
    Numeric,
    Date,
    Bool,
    Target,
    Drop,
}

/// <summary>Comparison operator. Not every operator is legal for every field. The matcher switches on this enum; the UI maps it to labels.</summary>
public enum FilterOperator
{
    Equals,
    NotEquals,
    Greater,
    Less,
    GreaterOrEqual,
    LessOrEqual,
    Contains,
    NotContains,
    IsTrue,
    IsFalse,
}

/// <summary>Closed union of typed filter values. No glob strings.</summary>
public abstract record FilterValue
{
    /// <summary>Enum code (ship / duration / mission-type / target spec name).</summary>
    public sealed record EnumValue(int Code) : FilterValue;

    /// <summary>Numeric value (level / capacity).</summary>
    public sealed record Number(double N) : FilterValue;

    /// <summary>Calendar day (launch / return date).</summary>
    public sealed record Day(DateOnly Date) : FilterValue;

    /// <summary>Boolean flag (dub-cap / bugged-cap); usually implicit via the operator.</summary>
    public sealed record Flag(bool On) : FilterValue;

    /// <summary>Structured drop spec.</summary>
    public sealed record Drop(DropMatch Match) : FilterValue;

    /// <summary>An unset value (blank editor row); never matches.</summary>
    public sealed record None : FilterValue
    {
        public static readonly None Instance = new();
    }
}

/// <summary>Structured replacement for the legacy "name_level_rarity_quality" glob; a null field means "any". Quality is the picked threshold the drop's mission config must reach (gated against the matched duration's range at match time); null = any.</summary>
public sealed record DropMatch(int? Name, int? Level, int? Rarity, double? Quality = null)
{
    public static readonly DropMatch Any = new(null, null, null);

    public static DropMatch AnyOfRarity(int rarity) => new(null, null, rarity);
}
