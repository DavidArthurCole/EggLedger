using EggLedger.Domain.MissionPacking;

namespace EggLedger.Web.Missions;

public enum MultiViewMode {
    Off,
    Row,
    Free,
}

public enum MissionSortMethod {
    Default,
    Iv,
}

/// <summary>Display flags default to and (de)serialize exactly as the Go storage settings do.</summary>
public sealed class MissionViewOptions {
    // Vue defaults (useMissionViewOptions.ts).
    public bool ViewByDate { get; set; }
    public bool ViewMissionTimes { get; set; } = true;
    public bool RecolorDc { get; set; }
    public bool RecolorBc { get; set; }
    public bool ShowExpectedDropsPerShip { get; set; } = true;
    public MultiViewMode MultiViewMode { get; set; } = MultiViewMode.Off;
    public MissionSortMethod SortMethod { get; set; } = MissionSortMethod.Default;

    /// <summary>null = All, 0 = Home, 1 = Virtue.</summary>
    public int? MissionTypeTab { get; set; }

    // Go storage setting keys (storage.go).
    public const string KeyViewByDate = "mission_view_by_date";
    public const string KeyViewTimes = "mission_view_times";
    public const string KeyRecolorDc = "mission_recolor_dc";
    public const string KeyRecolorBc = "mission_recolor_bc";
    public const string KeyShowExpectedDrops = "mission_show_expected_drops";
    public const string KeyMultiViewMode = "mission_multi_view_mode";
    public const string KeySortMethod = "mission_sort_method";

    /// <summary>True only when the loaded set contains at least one Home (0) and one Virtue (1) mission.</summary>
    public static bool HasBothMissionTypes(IReadOnlyList<DatabaseMission>? missions) {
        if (missions is null || missions.Count == 0) {
            return false;
        }
        bool home = false;
        bool virtue = false;
        foreach (var m in missions) {
            if (m.MissionType == 0) {
                home = true;
            } else if (m.MissionType == 1) {
                virtue = true;
            }
        }
        return home && virtue;
    }

    /// <summary>When no tab is selected (null) the input passes through unchanged; otherwise only missions whose type equals the tab.</summary>
    public static IReadOnlyList<DatabaseMission>? TabFilteredMissions(
        IReadOnlyList<DatabaseMission>? filteredMissions,
        int? missionTypeTab) {
        if (missionTypeTab is null || filteredMissions is null) {
            return filteredMissions;
        }
        var result = new List<DatabaseMission>();
        foreach (var m in filteredMissions) {
            if (m.MissionType == missionTypeTab.Value) {
                result.Add(m);
            }
        }
        return result;
    }

    public static MultiViewMode ParseMultiViewMode(string? raw) => raw switch {
        "row" => MultiViewMode.Row,
        "free" => MultiViewMode.Free,
        _ => MultiViewMode.Off,
    };

    public static string MultiViewModeToString(MultiViewMode mode) => mode switch {
        MultiViewMode.Row => "row",
        MultiViewMode.Free => "free",
        _ => "off",
    };

    public static MissionSortMethod ParseSortMethod(string? raw) =>
        raw == "iv" ? MissionSortMethod.Iv : MissionSortMethod.Default;

    public static string SortMethodToString(MissionSortMethod method) =>
        method == MissionSortMethod.Iv ? "iv" : "default";

    /// <summary>Hydrates display flags from a settings map; missing keys keep the defaults.</summary>
    public void LoadFrom(IReadOnlyDictionary<string, string> settings) {
        if (settings.TryGetValue(KeyViewByDate, out var vbd)) {
            ViewByDate = ParseBool(vbd, ViewByDate);
        }
        if (settings.TryGetValue(KeyViewTimes, out var vt)) {
            ViewMissionTimes = ParseBool(vt, ViewMissionTimes);
        }
        if (settings.TryGetValue(KeyRecolorDc, out var dc)) {
            RecolorDc = ParseBool(dc, RecolorDc);
        }
        if (settings.TryGetValue(KeyRecolorBc, out var bc)) {
            RecolorBc = ParseBool(bc, RecolorBc);
        }
        if (settings.TryGetValue(KeyShowExpectedDrops, out var sed)) {
            ShowExpectedDropsPerShip = ParseBool(sed, ShowExpectedDropsPerShip);
        }
        if (settings.TryGetValue(KeyMultiViewMode, out var mvm)) {
            MultiViewMode = ParseMultiViewMode(mvm);
        }
        if (settings.TryGetValue(KeySortMethod, out var sm)) {
            SortMethod = ParseSortMethod(sm);
        }
    }

    private static bool ParseBool(string raw, bool fallback) =>
        bool.TryParse(raw, out var v) ? v : fallback;
}
