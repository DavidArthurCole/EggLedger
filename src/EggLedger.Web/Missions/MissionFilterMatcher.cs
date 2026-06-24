using System.Globalization;
using EggLedger.Domain.MissionPacking;
using EggLedger.Domain.MissionQuery;
using EggLedger.Web.Missions.Model;

namespace EggLedger.Web.Missions;

/// <summary>Fetches the drops for a mission (seam for the Vue getShipDrops bridge). Returns null on cache miss / error, treated as "no match".</summary>
public delegate Task<IReadOnlyList<MissionDrop>?> ShipDropsFetcher(string accountId, string missionId);

/// <summary>Tests a DatabaseMission against a typed MissionFilter (OR of AND-groups). Pure except Drops, which is async (fetched per mission). Date compares are same-day Equals with inclusive GE/LE. The legacy string entry points convert via FilterCodec and run through the same core.</summary>
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

    /// <summary>Unix seconds -> local DateTime.</summary>
    public static DateTime LedgerDate(long timestampSeconds) =>
        DateTimeOffset.FromUnixTimeSeconds(timestampSeconds).LocalDateTime;

    // OR over groups, AND within a group. Empty filter matches everything. Drop conditions run last so cheap fields short-circuit before the async fetch.
    public async Task<bool> MatchesAsync(DatabaseMission mission, MissionFilter filter)
    {
        if (filter.IsEmpty)
        {
            return true;
        }
        foreach (var group in filter.Groups)
        {
            if (await GroupMatchesAsync(mission, group).ConfigureAwait(false))
            {
                return true;
            }
        }
        return false;
    }

    private async Task<bool> GroupMatchesAsync(DatabaseMission mission, FilterGroup group)
    {
        if (group.Conditions.Count == 0)
        {
            return true;
        }
        // Cheap (synchronous) conditions first; drops last.
        foreach (var c in group.Conditions)
        {
            if (c.Field != FilterField.Drops && !MatchesScalar(mission, c))
            {
                return false;
            }
        }
        foreach (var c in group.Conditions)
        {
            if (c.Field == FilterField.Drops && !await MatchesDropAsync(mission, c.Operator, DropOf(c.Value)).ConfigureAwait(false))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Single-condition test. Pure except for the Drops case.</summary>
    public async Task<bool> MatchesAsync(DatabaseMission mission, Condition condition)
    {
        if (condition.Field == FilterField.Drops)
        {
            return await MatchesDropAsync(mission, condition.Operator, DropOf(condition.Value)).ConfigureAwait(false);
        }
        return MatchesScalar(mission, condition);
    }

    private static DropMatch DropOf(FilterValue v) =>
        v is FilterValue.Drop d ? d.Match : DropMatch.Any;

    private bool MatchesScalar(DatabaseMission mission, Condition c)
    {
        return c.Field switch
        {
            FilterField.Ship => EnumMatch(EnumCode(mission.Ship), c),
            FilterField.DurationType => EnumMatch(EnumCode(mission.DurationType), c),
            FilterField.MissionType => EnumMatch(mission.MissionType, c),
            FilterField.Target => EnumMatch(mission.TargetInt, c),
            FilterField.Level => NumberMatch(mission.Level, c),
            FilterField.Capacity => NumberMatch(mission.Capacity, c),
            FilterField.LaunchDate => DateMatch(DateOnly.FromDateTime(LedgerDate(mission.LaunchDT)), c),
            FilterField.ReturnDate => DateMatch(DateOnly.FromDateTime(LedgerDate(mission.ReturnDT)), c),
            FilterField.DubCap => BoolMatch(mission.IsDubCap, c.Operator),
            FilterField.BuggedCap => BoolMatch(mission.IsBuggedCap, c.Operator),
            _ => true,
        };
    }

    private static int? EnumCode<T>(T? e) where T : struct, Enum =>
        e is null ? null : Convert.ToInt32(e.Value, CultureInfo.InvariantCulture);

    // null enum value: fails Equals, passes NotEquals. Ordering operators compare underlying enum codes (legacy filters allowed them).
    private static bool EnumMatch(int? missionValue, Condition c)
    {
        if (c.Value is not FilterValue.EnumValue e)
        {
            return false;
        }
        return c.Operator switch
        {
            FilterOperator.Equals => missionValue == e.Code,
            FilterOperator.NotEquals => missionValue != e.Code,
            FilterOperator.Greater => missionValue is { } mv && mv > e.Code,
            FilterOperator.Less => missionValue is { } mv && mv < e.Code,
            FilterOperator.GreaterOrEqual => missionValue is { } mv && mv >= e.Code,
            FilterOperator.LessOrEqual => missionValue is { } mv && mv <= e.Code,
            _ => false,
        };
    }

    private static bool NumberMatch(double missionValue, Condition c)
    {
        if (c.Value is not FilterValue.Number n)
        {
            return false;
        }
        return c.Operator switch
        {
            FilterOperator.Equals => missionValue == n.N,
            FilterOperator.NotEquals => missionValue != n.N,
            FilterOperator.Greater => missionValue > n.N,
            FilterOperator.Less => missionValue < n.N,
            FilterOperator.GreaterOrEqual => missionValue >= n.N,
            FilterOperator.LessOrEqual => missionValue <= n.N,
            _ => false,
        };
    }

    private static bool DateMatch(DateOnly missionDay, Condition c)
    {
        if (c.Value is not FilterValue.Day d)
        {
            return false;
        }
        return c.Operator switch
        {
            FilterOperator.Equals => missionDay == d.Date,
            FilterOperator.NotEquals => missionDay != d.Date,
            FilterOperator.Greater => missionDay > d.Date,
            FilterOperator.Less => missionDay < d.Date,
            FilterOperator.GreaterOrEqual => missionDay >= d.Date,
            FilterOperator.LessOrEqual => missionDay <= d.Date,
            _ => false,
        };
    }

    private static bool BoolMatch(bool missionValue, FilterOperator op) => op switch
    {
        FilterOperator.IsTrue => missionValue,
        FilterOperator.IsFalse => !missionValue,
        _ => false,
    };

    private async Task<bool> MatchesDropAsync(DatabaseMission mission, FilterOperator op, DropMatch m)
    {
        var shipConfig = mission.Ship is { } shipEnum
            ? FindShipConfig(Convert.ToInt32(shipEnum, CultureInfo.InvariantCulture))
            : null;
        if (shipConfig is null)
        {
            return false;
        }
        var durConfig = mission.DurationType is { } durEnum
            ? FindDurConfig(shipConfig, Convert.ToInt32(durEnum, CultureInfo.InvariantCulture))
            : null;
        if (durConfig is null)
        {
            return false;
        }

        // Quality gate: a picked quality threshold is reachable only within the matched duration's quality range for this mission level.
        if (m.Quality is { } q)
        {
            double maxQual = durConfig.MaxQuality + durConfig.LevelQualityBump * mission.Level;
            if (q > maxQual || durConfig.MinQuality > q)
            {
                return op == FilterOperator.NotContains;
            }
        }

        var allDrops = await _fetchDrops(_accountId, mission.MissiondId).ConfigureAwait(false);
        if (allDrops is null)
        {
            return false;
        }

        bool anySatisfies = false;
        foreach (var drop in allDrops)
        {
            if (m.Name is { } name && name != drop.Id)
            {
                continue;
            }
            if (m.Level is { } level && level != drop.Level)
            {
                continue;
            }
            if (m.Rarity is { } rarity && rarity != drop.Rarity)
            {
                continue;
            }
            anySatisfies = true;
            break;
        }

        return op == FilterOperator.NotContains ? !anySatisfies : anySatisfies;
    }

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

    // Compat shim for the legacy string filter API (the existing UI + tests).
    public async Task<bool> TestMissionAgainstFilterAsync(DatabaseMission mission, FilterCondition filter)
    {
        // Incomplete (missing field/op) -> no match.
        if (string.IsNullOrEmpty(filter.TopLevel) || string.IsNullOrEmpty(filter.Op))
        {
            return false;
        }
        var typed = FilterCodec.FromLegacyCondition(filter);
        if (typed is null)
        {
            // Unknown but well-formed field: impose no constraint, so it does not filter every mission out.
            return true;
        }
        return await MatchesAsync(mission, typed).ConfigureAwait(false);
    }

    // Legacy AND-list + per-index OR-sibling semantics (see docs/filter-legacy-semantics.md).
    public async Task<bool> MissionMatchesFilterAsync(
        DatabaseMission mission,
        IReadOnlyList<FilterCondition> filters,
        IReadOnlyList<IReadOnlyList<FilterCondition>?> orFilters)
    {
        for (var i = 0; i < filters.Count; i++)
        {
            if (FilterCodec.FromLegacyCondition(filters[i]) is not { } condition)
                continue;
            if (await MatchesAsync(mission, condition).ConfigureAwait(false))
                continue;

            var siblings = i < orFilters.Count ? orFilters[i] : null;
            if (!await AnySiblingMatchesAsync(mission, siblings).ConfigureAwait(false))
                return false;
        }
        return true;
    }

    private async Task<bool> AnySiblingMatchesAsync(DatabaseMission mission, IReadOnlyList<FilterCondition>? siblings)
    {
        if (siblings is null)
            return false;
        foreach (var sibling in siblings)
        {
            if (FilterCodec.FromLegacyCondition(sibling) is { } typed
                && await MatchesAsync(mission, typed).ConfigureAwait(false))
                return true;
        }
        return false;
    }
}
