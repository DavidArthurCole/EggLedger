using System.Globalization;
using EggLedger.Domain.MissionPacking;
using EggLedger.Domain.MissionQuery;
using EggLedger.Web.Missions.Model;

namespace EggLedger.Web.Missions;

public delegate Task<IReadOnlyList<MissionDrop>?> ShipDropsFetcher(string accountId, string missionId);

public sealed class MissionFilterMatcher {
    private readonly string _accountId;
    private readonly ShipDropsFetcher _fetchDrops;


    private readonly Dictionary<int, PossibleMission> _shipConfigs;
    private readonly Dictionary<int, Dictionary<int, DurationConfig>> _durByShip;

    public MissionFilterMatcher(
        IReadOnlyList<PossibleMission> durationConfigs,
        string? accountId,
        ShipDropsFetcher fetchDrops) {
        _accountId = accountId ?? "";
        _fetchDrops = fetchDrops;

        _shipConfigs = [];
        _durByShip = [];
        foreach (var pm in durationConfigs) {
            int ship = Convert.ToInt32(pm.Ship, CultureInfo.InvariantCulture);
            _shipConfigs[ship] = pm;
            var durs = new Dictionary<int, DurationConfig>();
            foreach (var d in pm.Durations) {
                durs[Convert.ToInt32(d.DurationType, CultureInfo.InvariantCulture)] = d;
            }
            _durByShip[ship] = durs;
        }
    }

    public static DateTime LedgerDate(long timestampSeconds) =>
        DateTimeOffset.FromUnixTimeSeconds(timestampSeconds).LocalDateTime;


    public async Task<bool> MatchesAsync(DatabaseMission mission, MissionFilter filter) {
        if (filter.IsEmpty) {
            return true;
        }
        foreach (var group in filter.Groups) {
            if (await GroupMatchesAsync(mission, group).ConfigureAwait(false)) {
                return true;
            }
        }
        return false;
    }

    private async Task<bool> GroupMatchesAsync(DatabaseMission mission, FilterGroup group) {
        if (group.Conditions.Count == 0) {
            return true;
        }

        foreach (var c in group.Conditions) {
            if (c.Field != FilterField.Drops && !MatchesScalar(mission, c)) {
                return false;
            }
        }
        foreach (var c in group.Conditions) {
            if (c.Field == FilterField.Drops && !await MatchesDropAsync(mission, c.Operator, DropOf(c.Value)).ConfigureAwait(false)) {
                return false;
            }
        }
        return true;
    }

    public async Task<bool> MatchesAsync(DatabaseMission mission, Condition condition) {
        if (condition.Field == FilterField.Drops) {
            return await MatchesDropAsync(mission, condition.Operator, DropOf(condition.Value)).ConfigureAwait(false);
        }
        return MatchesScalar(mission, condition);
    }

    private static DropMatch DropOf(FilterValue v) =>
        v is FilterValue.Drop d ? d.Match : DropMatch.Any;

    private bool MatchesScalar(DatabaseMission mission, Condition c) {
        return c.Field switch {
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


    private static bool EnumMatch(int? missionValue, Condition c) {
        if (c.Value is not FilterValue.EnumValue e) {
            return false;
        }
        return c.Operator switch {
            FilterOperator.Equals => missionValue == e.Code,
            FilterOperator.NotEquals => missionValue != e.Code,
            FilterOperator.Greater => missionValue is { } mv && mv > e.Code,
            FilterOperator.Less => missionValue is { } mv && mv < e.Code,
            FilterOperator.GreaterOrEqual => missionValue is { } mv && mv >= e.Code,
            FilterOperator.LessOrEqual => missionValue is { } mv && mv <= e.Code,
            _ => false,
        };
    }

    private static bool NumberMatch(double missionValue, Condition c) {
        if (c.Value is not FilterValue.Number n) {
            return false;
        }
        return c.Operator switch {
            FilterOperator.Equals => missionValue == n.N,
            FilterOperator.NotEquals => missionValue != n.N,
            FilterOperator.Greater => missionValue > n.N,
            FilterOperator.Less => missionValue < n.N,
            FilterOperator.GreaterOrEqual => missionValue >= n.N,
            FilterOperator.LessOrEqual => missionValue <= n.N,
            _ => false,
        };
    }

    private static bool DateMatch(DateOnly missionDay, Condition c) {
        if (c.Value is not FilterValue.Day d) {
            return false;
        }
        return c.Operator switch {
            FilterOperator.Equals => missionDay == d.Date,
            FilterOperator.NotEquals => missionDay != d.Date,
            FilterOperator.Greater => missionDay > d.Date,
            FilterOperator.Less => missionDay < d.Date,
            FilterOperator.GreaterOrEqual => missionDay >= d.Date,
            FilterOperator.LessOrEqual => missionDay <= d.Date,
            _ => false,
        };
    }

    private static bool BoolMatch(bool missionValue, FilterOperator op) => op switch {
        FilterOperator.IsTrue => missionValue,
        FilterOperator.IsFalse => !missionValue,
        _ => false,
    };

    private async Task<bool> MatchesDropAsync(DatabaseMission mission, FilterOperator op, DropMatch m) {
        var shipConfig = mission.Ship is { } shipEnum
            ? FindShipConfig(Convert.ToInt32(shipEnum, CultureInfo.InvariantCulture))
            : null;
        if (shipConfig is null) {
            return false;
        }
        var durConfig = mission.DurationType is { } durEnum
            ? FindDurConfig(shipConfig, Convert.ToInt32(durEnum, CultureInfo.InvariantCulture))
            : null;
        if (durConfig is null) {
            return false;
        }


        if (m.Quality is { } q) {
            double maxQual = durConfig.MaxQuality + durConfig.LevelQualityBump * mission.Level;
            if (q > maxQual || durConfig.MinQuality > q) {
                return op == FilterOperator.NotContains;
            }
        }

        var allDrops = await _fetchDrops(_accountId, mission.MissiondId).ConfigureAwait(false);
        if (allDrops is null) {
            return false;
        }

        bool anySatisfies = false;
        foreach (var drop in allDrops) {
            if (m.Name is { } name && name != drop.Id) {
                continue;
            }
            if (m.Level is { } level && level != drop.Level) {
                continue;
            }
            if (m.Rarity is { } rarity && rarity != drop.Rarity) {
                continue;
            }
            anySatisfies = true;
            break;
        }

        return op == FilterOperator.NotContains ? !anySatisfies : anySatisfies;
    }

    private PossibleMission? FindShipConfig(int ship) =>
        _shipConfigs.GetValueOrDefault(ship);

    private DurationConfig? FindDurConfig(PossibleMission ship, int duration) {
        int shipKey = Convert.ToInt32(ship.Ship, CultureInfo.InvariantCulture);
        return _durByShip.TryGetValue(shipKey, out var durs) && durs.TryGetValue(duration, out var d) ? d : null;
    }


    public async Task<bool> TestMissionAgainstFilterAsync(DatabaseMission mission, FilterCondition filter) {

        if (string.IsNullOrEmpty(filter.TopLevel) || string.IsNullOrEmpty(filter.Op)) {
            return false;
        }
        var typed = FilterCodec.FromLegacyCondition(filter);
        if (typed is null) {

            return true;
        }
        return await MatchesAsync(mission, typed).ConfigureAwait(false);
    }


    public async Task<bool> MissionMatchesFilterAsync(
        DatabaseMission mission,
        IReadOnlyList<FilterCondition> filters,
        IReadOnlyList<IReadOnlyList<FilterCondition>?> orFilters) {
        for (var i = 0; i < filters.Count; i++) {
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

    private async Task<bool> AnySiblingMatchesAsync(DatabaseMission mission, IReadOnlyList<FilterCondition>? siblings) {
        if (siblings is null)
            return false;
        foreach (var sibling in siblings) {
            if (FilterCodec.FromLegacyCondition(sibling) is { } typed
                && await MatchesAsync(mission, typed).ConfigureAwait(false)) {
                return true;
            }
        }
        return false;
    }
}
