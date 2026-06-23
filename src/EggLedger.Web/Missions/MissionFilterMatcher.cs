using System.Globalization;
using EggLedger.Domain.MissionPacking;
using EggLedger.Domain.MissionQuery;

namespace EggLedger.Web.Missions;

/// <summary>
/// Fetches the drops for a mission. C# seam for the Vue
/// <c>globalThis.getShipDrops(accountId, missionId)</c> bridge call the drops
/// filter makes. Returns null on cache miss / error (Vue treats both as "no
/// match").
/// </summary>
public delegate Task<IReadOnlyList<MissionDrop>?> ShipDropsFetcher(string accountId, string missionId);

/// <summary>
/// Tests a <see cref="DatabaseMission"/> against a compiled filter tree. C# port
/// of www/src/composables/useFilterMatching.ts (the core predicate). AND
/// conditions with OR siblings: a mission passes when every AND condition passes,
/// or, for any failing AND condition, one of its OR siblings passes ("and-only"
/// fallback). The drops case is async because it fetches drop data via the
/// injected <see cref="ShipDropsFetcher"/>.
///
/// <para>JS comparison semantics are preserved exactly, including: loose
/// <c>==</c>/<c>!=</c> between the numeric mission field and the string filter
/// value; the date <c>=</c> operator comparing two distinct Date objects by
/// reference (always unequal, so <c>=</c> on a date never matches); and the
/// date <c>&lt;=</c>/<c>&gt;=</c> operators falling through to the no-op default
/// (always pass).</para>
/// </summary>
public sealed class MissionFilterMatcher
{
    private readonly IReadOnlyList<PossibleMission> _durationConfigs;
    private readonly string _accountId;
    private readonly ShipDropsFetcher _fetchDrops;

    public MissionFilterMatcher(
        IReadOnlyList<PossibleMission> durationConfigs,
        string? accountId,
        ShipDropsFetcher fetchDrops)
    {
        _durationConfigs = durationConfigs;
        _accountId = accountId ?? "";
        _fetchDrops = fetchDrops;
    }

    /// <summary>Port of ledgerDate: Unix seconds -> local DateTime (Date in JS).</summary>
    public static DateTime LedgerDate(long timestampSeconds) =>
        DateTimeOffset.FromUnixTimeSeconds(timestampSeconds).LocalDateTime;

    /// <summary>
    /// Port of ledgerDateObj: "YYYY-MM-DD" -> local midnight DateTime. Mirrors the
    /// JS <c>new Date(year, monthIndex, day)</c> constructor (local time).
    /// </summary>
    public static DateTime LedgerDateObj(string date)
    {
        var parts = date.Split('-');
        int year = int.Parse(parts[0], CultureInfo.InvariantCulture);
        int month = int.Parse(parts[1], CultureInfo.InvariantCulture);
        int day = int.Parse(parts[2], CultureInfo.InvariantCulture);
        return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Local);
    }

    // Tagged comparison operands so JS loose equality and the date reference-bug
    // can be reproduced exactly. A "string number" value is the mission field
    // rendered as JS would render it (a number); the filter value is the raw
    // string the user picked.

    private enum OperandKind { Number, Bool, Date, Null }

    private readonly record struct Operand(OperandKind Kind, double Num, bool Bool, DateTime Date)
    {
        public static Operand OfNumber(double n) => new(OperandKind.Number, n, false, default);
        public static Operand OfBool(bool b) => new(OperandKind.Bool, 0, b, default);
        public static Operand OfDate(DateTime d) => new(OperandKind.Date, 0, false, d);
        public static readonly Operand OfNull = new(OperandKind.Null, 0, false, default);
    }

    /// <summary>
    /// Port of commonFilterLogic. Returns the (unchanged) currentState when the
    /// condition holds; false otherwise. For unknown operators it returns
    /// currentState unchanged (JS default branch).
    /// </summary>
    private static bool CommonFilterLogic(Operand value, Operand filterValue, string op, bool currentState)
    {
        switch (op)
        {
            case "d=":
                // toDateString() equality on two Date operands.
                if (value.Kind == OperandKind.Date && filterValue.Kind == OperandKind.Date)
                {
                    if (value.Date.Date != filterValue.Date.Date)
                    {
                        return false;
                    }
                }
                break;
            case "=":
                if (!LooseEquals(value, filterValue))
                {
                    return false;
                }
                break;
            case "!=":
                if (LooseEquals(value, filterValue))
                {
                    return false;
                }
                break;
            case ">":
                if (ToNumber(value) <= ToNumber(filterValue))
                {
                    return false;
                }
                break;
            case "<":
                if (ToNumber(value) >= ToNumber(filterValue))
                {
                    return false;
                }
                break;
            case "true":
                if (!Truthy(value))
                {
                    return false;
                }
                break;
            case "false":
                if (Truthy(value))
                {
                    return false;
                }
                break;
            default:
                return currentState;
        }
        return currentState;
    }

    // JS == / != semantics for the operand pairs this filter produces.
    private static bool LooseEquals(Operand a, Operand b)
    {
        // Date "=" compares two distinct Date object references in JS: always
        // unequal. Reproduce that exactly (the date "=" operator never matches).
        if (a.Kind == OperandKind.Date || b.Kind == OperandKind.Date)
        {
            return false;
        }
        if (a.Kind == OperandKind.Null || b.Kind == OperandKind.Null)
        {
            // value != filterValue with one side null: equal only if both null.
            return a.Kind == OperandKind.Null && b.Kind == OperandKind.Null;
        }
        return ToNumber(a) == ToNumber(b);
    }

    private static double ToNumber(Operand o) => o.Kind switch
    {
        OperandKind.Number => o.Num,
        OperandKind.Date => o.Date.Ticks, // relational coercion of Date -> number
        OperandKind.Bool => o.Bool ? 1 : 0,
        _ => double.NaN,
    };

    private static bool Truthy(Operand o) => o.Kind switch
    {
        OperandKind.Bool => o.Bool,
        OperandKind.Number => o.Num != 0,
        OperandKind.Date => true,
        _ => false,
    };

    private static Operand NumberFromString(string s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? Operand.OfNumber(v)
            : Operand.OfNull;

    /// <summary>
    /// Tests a single condition against a mission. Port of
    /// testMissionAgainstFilter. Returns false when the condition is incomplete
    /// (mirrors the Vue guard returning false).
    /// </summary>
    public async Task<bool> TestMissionAgainstFilterAsync(DatabaseMission mission, FilterCondition filter)
    {
        // Vue guard: topLevel/op/val all non-null. Val is a non-null string here,
        // so only a missing topLevel/op short-circuits to "no match".
        if (string.IsNullOrEmpty(filter.TopLevel) || string.IsNullOrEmpty(filter.Op))
        {
            return false;
        }

        const bool start = true;

        switch (filter.TopLevel)
        {
            case "buggedcap":
                // Vue passes filter.val as the OPERATOR ("true"/"false"), null value.
                return CommonFilterLogic(Operand.OfBool(mission.IsBuggedCap), Operand.OfNull, filter.Val, start);
            case "dubcap":
                return CommonFilterLogic(Operand.OfBool(mission.IsDubCap), Operand.OfNull, filter.Val, start);
            case "ship":
                return CommonFilterLogic(NumberOrNull(mission.Ship), NumberFromString(filter.Val), filter.Op, start);
            case "farm":
            case "type":
                return CommonFilterLogic(Operand.OfNumber(mission.MissionType), NumberFromString(filter.Val), filter.Op, start);
            case "duration":
                return CommonFilterLogic(NumberOrNull(mission.DurationType), NumberFromString(filter.Val), filter.Op, start);
            case "level":
                return CommonFilterLogic(Operand.OfNumber(mission.Level), NumberFromString(filter.Val), filter.Op, start);
            case "target":
                return CommonFilterLogic(Operand.OfNumber(mission.TargetInt), NumberFromString(filter.Val), filter.Op, start);
            case "launchDT":
                return CommonFilterLogic(
                    Operand.OfDate(LedgerDate(mission.LaunchDT)),
                    Operand.OfDate(LedgerDateObj(filter.Val)),
                    filter.Op, start);
            case "returnDT":
                return CommonFilterLogic(
                    Operand.OfDate(LedgerDate(mission.ReturnDT)),
                    Operand.OfDate(LedgerDateObj(filter.Val)),
                    filter.Op, start);
            case "drops":
                return await TestDropsAsync(mission, filter);
            default:
                return start;
        }
    }

    private static Operand NumberOrNull<T>(T? enumValue) where T : struct, Enum =>
        enumValue is null ? Operand.OfNull : Operand.OfNumber(Convert.ToInt32(enumValue.Value, CultureInfo.InvariantCulture));

    private async Task<bool> TestDropsAsync(DatabaseMission mission, FilterCondition filter)
    {
        bool filterPassed = true;

        var shipConfig = mission.Ship is { } shipEnum
            ? FindShipConfig((int)Convert.ToInt32(shipEnum, CultureInfo.InvariantCulture))
            : null;
        if (shipConfig is null)
        {
            return false;
        }
        var durConfig = mission.DurationType is { } durEnum
            ? FindDurConfig(shipConfig, (int)Convert.ToInt32(durEnum, CultureInfo.InvariantCulture))
            : null;
        if (durConfig is null)
        {
            return false;
        }

        double maxQual = durConfig.MaxQuality + (durConfig.LevelQualityBump * mission.Level);

        var segs = filter.Val.Split('_');
        string filterName = segs.Length > 0 ? segs[0] : "";
        string filterLevel = segs.Length > 1 ? segs[1] : "";
        string filterRarity = segs.Length > 2 ? segs[2] : "";
        string filterQuality = segs.Length > 3 ? segs[3] : "";
        bool nameBypass = filterName == "%";
        bool levelBypass = filterLevel == "%";
        bool rarityBypass = filterRarity == "%";
        bool qualityBypass = filterQuality == "%";

        var allDrops = await _fetchDrops(_accountId, mission.MissiondId);
        if (allDrops is null)
        {
            return false;
        }

        switch (filter.Op)
        {
            case "c":
            {
                bool foundDrop = false;
                if ((!qualityBypass && maxQual < ParseFloat(filterQuality)) ||
                    (!qualityBypass && durConfig.MinQuality > ParseFloat(filterQuality)))
                {
                    filterPassed = false;
                }
                foreach (var drop in allDrops)
                {
                    if (!nameBypass && ParseInt(filterName) != drop.Id)
                    {
                        continue;
                    }
                    if (!levelBypass && ParseInt(filterLevel) != drop.Level)
                    {
                        continue;
                    }
                    if (!rarityBypass && ParseInt(filterRarity) != drop.Rarity)
                    {
                        continue;
                    }
                    foundDrop = true;
                }
                if (!foundDrop)
                {
                    filterPassed = false;
                }
                break;
            }
            case "dnc":
            {
                foreach (var drop in allDrops)
                {
                    if (!nameBypass && ParseInt(filterName) != drop.Id)
                    {
                        continue;
                    }
                    if (!levelBypass && ParseInt(filterLevel) != drop.Level)
                    {
                        continue;
                    }
                    if (!rarityBypass && ParseInt(filterRarity) != drop.Rarity)
                    {
                        continue;
                    }
                    filterPassed = false;
                }
                break;
            }
        }

        return filterPassed;
    }

    // Vue indexes durationConfigs.value[mission.ship]; here we resolve by enum
    // value rather than array index so ordering changes cannot silently misalign.
    private PossibleMission? FindShipConfig(int ship)
    {
        foreach (var pm in _durationConfigs)
        {
            if (Convert.ToInt32(pm.Ship, CultureInfo.InvariantCulture) == ship)
            {
                return pm;
            }
        }
        return null;
    }

    private static DurationConfig? FindDurConfig(PossibleMission ship, int duration)
    {
        foreach (var d in ship.Durations)
        {
            if (Convert.ToInt32(d.DurationType, CultureInfo.InvariantCulture) == duration)
            {
                return d;
            }
        }
        return null;
    }

    private static double ParseFloat(string s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN;

    private static int ParseInt(string s)
    {
        // JS Number.parseInt: leading integer prefix; NaN -> never equals an int field.
        int i = 0;
        bool neg = false;
        if (i < s.Length && (s[i] == '+' || s[i] == '-'))
        {
            neg = s[i] == '-';
            i++;
        }
        int start = i;
        while (i < s.Length && s[i] >= '0' && s[i] <= '9')
        {
            i++;
        }
        if (i == start)
        {
            return int.MinValue; // stands in for NaN: will not equal any real drop field
        }
        int value = int.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
        return neg ? -value : value;
    }

    /// <summary>
    /// Tests a mission against the full AND/OR filter set. Port of
    /// missionMatchesFilter. A mission passes when every AND condition passes; a
    /// failing AND condition is rescued if any of its OR siblings passes.
    /// </summary>
    public async Task<bool> MissionMatchesFilterAsync(
        DatabaseMission mission,
        IReadOnlyList<FilterCondition> filters,
        IReadOnlyList<IReadOnlyList<FilterCondition>?> orFilters)
    {
        bool allFiltersPassed = true;
        int index = 0;
        foreach (var filter in filters)
        {
            if (IsComplete(filter))
            {
                bool filterPassed = await TestMissionAgainstFilterAsync(mission, filter);
                if (!filterPassed)
                {
                    var siblings = index < orFilters.Count ? orFilters[index] : null;
                    if (siblings is not null)
                    {
                        foreach (var orFilter in siblings)
                        {
                            if (IsComplete(orFilter))
                            {
                                filterPassed = await TestMissionAgainstFilterAsync(mission, orFilter);
                                if (filterPassed)
                                {
                                    break;
                                }
                            }
                        }
                    }
                    if (!filterPassed)
                    {
                        allFiltersPassed = false;
                    }
                }
            }
            index++;
        }
        return allFiltersPassed;
    }

    // Vue completeness guard: topLevel != null && op != null && (val != null ||
    // topLevel === 'target'). In Vue an empty-string val still satisfies
    // "val != null", so only a missing topLevel/op makes a condition incomplete
    // (Val is a non-null string here, modelling JS's non-null branch).
    private static bool IsComplete(FilterCondition f) =>
        !string.IsNullOrEmpty(f.TopLevel) && f.Op is not null && f.Op.Length > 0;
}
