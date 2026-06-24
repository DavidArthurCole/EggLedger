using EggLedger.Domain.MissionPacking;
using EggLedger.Domain.MissionQuery;
using EggLedger.Web.Services;

namespace EggLedger.Web.Missions;

/// <summary>Menno community-data summary attached to a viewed mission (configs + total drop count).</summary>
public sealed class MissionMennoData {
    public int TotalDropsCount { get; set; }
    public IReadOnlyList<ConfigurationItem> Configs { get; set; } =
        Array.Empty<ConfigurationItem>();
}

/// <summary>Computed detail for one viewed mission: drop lists grouped + sorted, metadata (capacity modifier, prev/next ids, dates) derived once.</summary>
public sealed class ViewMissionData {
    public DatabaseMission MissionInfo { get; set; } = new();
    public List<DropLike> Artifacts { get; set; } = [];
    public List<DropLike> Stones { get; set; } = [];
    public List<DropLike> StoneFragments { get; set; } = [];
    public List<DropLike> Ingredients { get; set; } = [];
    public DateTime LaunchDT { get; set; }
    public DateTime ReturnDT { get; set; }
    public string DurationStr { get; set; } = "";
    public double CapacityModifier { get; set; }
    public string? PrevMission { get; set; }
    public string? NextMission { get; set; }
    public MissionMennoData MennoData { get; set; } = new();
}

/// <summary>Pure mission-detail computation: shapes already-fetched data only. Async overlay state, bridge fetches, and caching stay in the Blazor component.</summary>
public static class MissionDetailBuilder {
    private static DropLike ToDropLike(MissionDrop d) => new() {
        Id = d.Id,
        Name = d.Name,
        Level = d.Level,
        Rarity = d.Rarity,
        Quality = d.Quality,
        IvOrder = d.IVOrder,
        SpecType = d.SpecType,
    };

    /// <summary>Builds the base ViewMissionData: split drops by spec type, group + sort each list, derive dates, capacity modifier, prev/next ids. Initially grouped with sortedGroupedSpecType; ApplySortMethod re-sorts per the active method.</summary>
    public static ViewMissionData BuildBase(
        DatabaseMission missionInfo,
        IReadOnlyList<MissionDrop> allDrops,
        IReadOnlyList<DatabaseMission> filteredMissions,
        bool extendedInfo) {
        var artifacts = new List<DropLike>();
        var stones = new List<DropLike>();
        var stoneFragments = new List<DropLike>();
        var ingredients = new List<DropLike>();
        foreach (var d in allDrops) {
            switch (d.SpecType) {
                case "Artifact": artifacts.Add(ToDropLike(d)); break;
                case "Stone": stones.Add(ToDropLike(d)); break;
                case "StoneFragment": stoneFragments.Add(ToDropLike(d)); break;
                case "Ingredient": ingredients.Add(ToDropLike(d)); break;
            }
        }

        int shipIndex = -1;
        if (extendedInfo) {
            for (int i = 0; i < filteredMissions.Count; i++) {
                if (filteredMissions[i].MissiondId == missionInfo.MissiondId) {
                    shipIndex = i;
                    break;
                }
            }
        }

        double nominal = missionInfo.NominalCapcity != 0 ? missionInfo.NominalCapcity : 1;

        return new ViewMissionData {
            MissionInfo = missionInfo,
            Artifacts = DropSorter.SortedGroupedSpecType(artifacts),
            Stones = DropSorter.SortedGroupedSpecType(stones),
            StoneFragments = DropSorter.SortedGroupedSpecType(stoneFragments),
            Ingredients = DropSorter.SortedGroupedSpecType(ingredients),
            LaunchDT = MissionFilterMatcher.LedgerDate(missionInfo.LaunchDT),
            ReturnDT = MissionFilterMatcher.LedgerDate(missionInfo.ReturnDT),
            DurationStr = missionInfo.DurationString,
            CapacityModifier = Math.Min(2, missionInfo.Capacity / nominal),
            PrevMission = extendedInfo && shipIndex > 0 ? filteredMissions[shipIndex - 1].MissiondId : null,
            NextMission = extendedInfo && shipIndex >= 0 && shipIndex < filteredMissions.Count - 1
                ? filteredMissions[shipIndex + 1].MissiondId
                : null,
            MennoData = new MissionMennoData(),
        };
    }

    /// <summary>Re-sorts the already-grouped drop lists: iv -> inventoryVisualizerSort, otherwise sortGroupAlreadyCombed.</summary>
    public static void ApplySortMethod(ViewMissionData data, MissionSortMethod method) {
        Func<IEnumerable<DropLike>, List<DropLike>> sortFn = method == MissionSortMethod.Iv
            ? DropSorter.InventoryVisualizerSort
            : DropSorter.SortGroupAlreadyCombed;
        data.Artifacts = sortFn(data.Artifacts);
        data.Stones = sortFn(data.Stones);
        data.StoneFragments = sortFn(data.StoneFragments);
        data.Ingredients = sortFn(data.Ingredients);
    }

    /// <summary>Menno lookup key for a mission: target -1 remapped to 10000, key is ship_duration_level_target.</summary>
    public static string MennoKey(DatabaseMission mission) {
        int target = mission.TargetInt == -1 ? 10000 : mission.TargetInt;
        int ship = mission.Ship is { } s ? Convert.ToInt32(s) : 0;
        int duration = mission.DurationType is { } d ? Convert.ToInt32(d) : 0;
        return $"{ship}_{duration}_{mission.Level}_{target}";
    }
}
