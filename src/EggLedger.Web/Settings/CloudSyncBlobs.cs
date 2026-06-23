using System.Text.Json.Serialization;
using EggLedger.Domain.MissionQuery;
using EggLedger.Domain.Reports;
using EggLedger.Web.Data;

namespace EggLedger.Web.Settings;

/// <summary>
/// The cloud-syncable settings subset. C# port of the Go cloudsync
/// <c>cloudSyncableSettings</c> struct (cloudsync/blob.go): the machine-agnostic
/// settings safe to sync across devices (excludes paths, resolution, browser, and
/// other machine-local prefs). The JSON property names are the frozen blob
/// contract and must match Go byte-for-byte so a blob written by the desktop app
/// round-trips in the browser and vice versa.
/// </summary>
public sealed record CloudSyncableSettings
{
    [JsonPropertyName("auto_refresh_menno_pref")]
    public bool AutoRefreshMennoPref { get; init; }

    [JsonPropertyName("retry_failed_missions")]
    public bool RetryFailedMissions { get; init; }

    [JsonPropertyName("hide_timeout_errors")]
    public bool HideTimeoutErrors { get; init; }

    [JsonPropertyName("worker_count")]
    public int WorkerCount { get; init; }

    [JsonPropertyName("screenshot_safety")]
    public bool ScreenshotSafety { get; init; }

    [JsonPropertyName("show_mission_progress")]
    public bool ShowMissionProgress { get; init; }

    [JsonPropertyName("collapse_older_sections")]
    public bool CollapseOlderSections { get; init; }

    [JsonPropertyName("advanced_drop_filter")]
    public bool AdvancedDropFilter { get; init; }

    [JsonPropertyName("mission_view_by_date")]
    public bool MissionViewByDate { get; init; }

    [JsonPropertyName("mission_view_times")]
    public bool MissionViewTimes { get; init; }

    [JsonPropertyName("mission_recolor_dc")]
    public bool MissionRecolorDC { get; init; }

    [JsonPropertyName("mission_recolor_bc")]
    public bool MissionRecolorBC { get; init; }

    [JsonPropertyName("mission_show_expected_drops")]
    public bool MissionShowExpectedDrops { get; init; }

    [JsonPropertyName("mission_multi_view_mode")]
    public string MissionMultiViewMode { get; init; } = "";

    [JsonPropertyName("mission_sort_method")]
    public string MissionSortMethod { get; init; } = "";

    [JsonPropertyName("lifetime_sort_method")]
    public string LifetimeSortMethod { get; init; } = "";

    [JsonPropertyName("lifetime_show_drops_per_ship")]
    public bool LifetimeShowDropsPerShip { get; init; }

    [JsonPropertyName("lifetime_show_expected_totals")]
    public bool LifetimeShowExpectedTotals { get; init; }
}

/// <summary>
/// One group entry in the "reports" blob. C# port of the Go
/// <c>reportdb.ReportGroupRow</c> (reportdb/groups.go). That Go struct carries NO
/// json tags, so Go's default marshal emits PascalCase field names. To byte-match
/// the cross-device contract the JSON keys here are pinned PascalCase
/// (<c>Id</c>, <c>AccountId</c>, <c>Name</c>, <c>SortOrder</c>, <c>CreatedAt</c>),
/// NOT the snake_case the local IndexedDB store uses. Map to/from the local
/// <see cref="ReportGroupRow"/> at the blob boundary.
/// </summary>
public sealed record CloudReportGroup
{
    [JsonPropertyName("Id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("AccountId")]
    public string AccountId { get; init; } = "";

    [JsonPropertyName("Name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("SortOrder")]
    public int SortOrder { get; init; }

    [JsonPropertyName("CreatedAt")]
    public long CreatedAt { get; init; }

    /// <summary>Maps a local IndexedDB group row to its Go wire shape.</summary>
    public static CloudReportGroup FromRow(ReportGroupRow r) => new()
    {
        Id = r.Id,
        AccountId = r.AccountId,
        Name = r.Name,
        SortOrder = r.SortOrder,
        CreatedAt = r.CreatedAt,
    };

    /// <summary>Maps a Go wire group back to a local IndexedDB group row.</summary>
    public ReportGroupRow ToRow() => new()
    {
        Id = Id,
        AccountId = AccountId,
        Name = Name,
        SortOrder = SortOrder,
        CreatedAt = CreatedAt,
    };
}

/// <summary>
/// The "reports" blob payload. C# port of the Go <c>cloudReportsBlob</c>
/// (cloudsync/blob.go): all reports and groups across every known account. The
/// blob is a FROZEN cross-device contract, so the wire shape mirrors Go exactly:
/// <list type="bullet">
/// <item>top-level keys are <c>reports</c> / <c>groups</c> (the Go struct's json tags),</item>
/// <item>each report is a <see cref="ReportDefinition"/> (camelCase keys, nested
/// <c>filters</c> object), matching Go <c>reports.ReportDefinition</c>, NOT the
/// snake_case IndexedDB <see cref="ReportRow"/> whose <c>filters</c> is a string,</item>
/// <item>each group is a <see cref="CloudReportGroup"/> (PascalCase keys), matching
/// the untagged Go <c>reportdb.ReportGroupRow</c>.</item>
/// </list>
/// Use <see cref="ReportMapping"/> to convert local <see cref="ReportRow"/>s to/from
/// the <see cref="ReportDefinition"/> wire form (it parses/serializes the filters).
/// </summary>
public sealed record CloudReportsBlob
{
    [JsonPropertyName("reports")]
    public IReadOnlyList<ReportDefinition> Reports { get; init; } = [];

    [JsonPropertyName("groups")]
    public IReadOnlyList<CloudReportGroup> Groups { get; init; } = [];

    /// <summary>
    /// Packs local IndexedDB rows into the Go-shaped blob: each report row becomes a
    /// <see cref="ReportDefinition"/> (filters string -> nested object) via
    /// <see cref="ReportMapping.ToDefinition"/>, each group row becomes a
    /// <see cref="CloudReportGroup"/>.
    /// </summary>
    public static CloudReportsBlob Pack(
        IReadOnlyList<ReportRow> reports, IReadOnlyList<ReportGroupRow> groups) => new()
    {
        Reports = reports.Select(ReportMapping.ToDefinition).ToList(),
        Groups = groups.Select(CloudReportGroup.FromRow).ToList(),
    };
}

/// <summary>
/// Pure pack/unpack assembly for the three cloud-sync blobs (<c>accounts</c>,
/// <c>settings</c>, <c>reports</c>). C# port of the pure parts of
/// www/src/composables/useCloudSync.ts + the Go cloudsync sync-up / restore-down
/// flows: which blob carries what, the syncable-settings projection, and the
/// merge rules on restore. The HTTP transport and the poll loop live in the
/// Settings cloud-sync panel; this class only shapes the payloads.
/// </summary>
public static class CloudSyncBlobs
{
    /// <summary>The three frozen blob names, matching Go putBlob/getBlob.</summary>
    public const string AccountsBlob = "accounts";
    public const string SettingsBlob = "settings";
    public const string ReportsBlob = "reports";

    /// <summary>
    /// Projects the cloud-syncable subset out of the full settings map. Mirrors the
    /// Go runSyncToCloud settings-blob assembly: only the machine-agnostic keys are
    /// included, each read with the same default the desktop app uses when the key
    /// is absent.
    /// </summary>
    public static CloudSyncableSettings PackSettings(IReadOnlyDictionary<string, string> settings) => new()
    {
        AutoRefreshMennoPref = Bool(settings, SettingsModel.KeyAutoRefreshMenno, false),
        RetryFailedMissions = Bool(settings, SettingsModel.KeyRetryFailedMissions, false),
        HideTimeoutErrors = Bool(settings, SettingsModel.KeyHideTimeoutErrors, false),
        WorkerCount = SettingsModel.ClampWorkerCount(Int(settings, SettingsModel.KeyWorkerCount, SettingsModel.MinWorkerCount)),
        ScreenshotSafety = Bool(settings, SettingsModel.KeyScreenshotSafety, false),
        ShowMissionProgress = Bool(settings, SettingsModel.KeyShowMissionProgress, true),
        CollapseOlderSections = Bool(settings, SettingsModel.KeyCollapseOlderSections, true),
        AdvancedDropFilter = Bool(settings, SettingsModel.KeyAdvancedDropFilter, false),
        MissionViewByDate = Bool(settings, "mission_view_by_date", false),
        MissionViewTimes = Bool(settings, "mission_view_times", true),
        MissionRecolorDC = Bool(settings, "mission_recolor_dc", false),
        MissionRecolorBC = Bool(settings, "mission_recolor_bc", false),
        MissionShowExpectedDrops = Bool(settings, "mission_show_expected_drops", true),
        MissionMultiViewMode = Str(settings, "mission_multi_view_mode", "off"),
        MissionSortMethod = Str(settings, "mission_sort_method", "default"),
        LifetimeSortMethod = Str(settings, "lifetime_sort_method", ""),
        LifetimeShowDropsPerShip = Bool(settings, "lifetime_show_drops_per_ship", false),
        LifetimeShowExpectedTotals = Bool(settings, "lifetime_show_expected_totals", false),
    };

    /// <summary>
    /// Turns a restored settings blob into the key/value pairs to upsert into the
    /// settings store. Mirrors Go applyCloudSettings: writes only the syncable keys
    /// (machine-local prefs like resolution and browser path are untouched).
    /// </summary>
    public static IReadOnlyDictionary<string, string> UnpackSettings(CloudSyncableSettings s) => new Dictionary<string, string>
    {
        [SettingsModel.KeyAutoRefreshMenno] = SettingsModel.FormatBool(s.AutoRefreshMennoPref),
        [SettingsModel.KeyRetryFailedMissions] = SettingsModel.FormatBool(s.RetryFailedMissions),
        [SettingsModel.KeyHideTimeoutErrors] = SettingsModel.FormatBool(s.HideTimeoutErrors),
        [SettingsModel.KeyWorkerCount] = SettingsModel.FormatInt(SettingsModel.ClampWorkerCount(s.WorkerCount)),
        [SettingsModel.KeyScreenshotSafety] = SettingsModel.FormatBool(s.ScreenshotSafety),
        [SettingsModel.KeyShowMissionProgress] = SettingsModel.FormatBool(s.ShowMissionProgress),
        [SettingsModel.KeyCollapseOlderSections] = SettingsModel.FormatBool(s.CollapseOlderSections),
        [SettingsModel.KeyAdvancedDropFilter] = SettingsModel.FormatBool(s.AdvancedDropFilter),
        ["mission_view_by_date"] = SettingsModel.FormatBool(s.MissionViewByDate),
        ["mission_view_times"] = SettingsModel.FormatBool(s.MissionViewTimes),
        ["mission_recolor_dc"] = SettingsModel.FormatBool(s.MissionRecolorDC),
        ["mission_recolor_bc"] = SettingsModel.FormatBool(s.MissionRecolorBC),
        ["mission_show_expected_drops"] = SettingsModel.FormatBool(s.MissionShowExpectedDrops),
        ["mission_multi_view_mode"] = s.MissionMultiViewMode,
        ["mission_sort_method"] = s.MissionSortMethod,
        ["lifetime_sort_method"] = s.LifetimeSortMethod,
        ["lifetime_show_drops_per_ship"] = SettingsModel.FormatBool(s.LifetimeShowDropsPerShip),
        ["lifetime_show_expected_totals"] = SettingsModel.FormatBool(s.LifetimeShowExpectedTotals),
    };

    /// <summary>
    /// Selects the report/group rows to insert on restore: each row whose id is not
    /// already present locally, de-duplicated by id (first wins) and skipping blank
    /// ids. Mirrors Go importRemoteReports's seen-set guard.
    /// </summary>
    public static (List<ReportGroupRow> Groups, List<ReportRow> Reports) SelectReportsToImport(
        CloudReportsBlob remote,
        IReadOnlyCollection<string> existingGroupIds,
        IReadOnlyCollection<string> existingReportIds)
    {
        var groups = new List<ReportGroupRow>();
        var seenGroups = new HashSet<string>(existingGroupIds, StringComparer.Ordinal);
        foreach (var g in remote.Groups)
        {
            if (string.IsNullOrEmpty(g.Id) || !seenGroups.Add(g.Id))
            {
                continue;
            }
            groups.Add(g.ToRow());
        }

        var reports = new List<ReportRow>();
        var seenReports = new HashSet<string>(existingReportIds, StringComparer.Ordinal);
        foreach (var d in remote.Reports)
        {
            if (string.IsNullOrEmpty(d.Id) || !seenReports.Add(d.Id))
            {
                continue;
            }
            reports.Add(ReportMapping.ToRow(d));
        }

        return (groups, reports);
    }

    private static bool Bool(IReadOnlyDictionary<string, string> s, string key, bool fallback) =>
        s.TryGetValue(key, out var raw) && bool.TryParse(raw, out var v) ? v : fallback;

    private static int Int(IReadOnlyDictionary<string, string> s, string key, int fallback) =>
        s.TryGetValue(key, out var raw) && int.TryParse(raw, out var v) ? v : fallback;

    private static string Str(IReadOnlyDictionary<string, string> s, string key, string fallback) =>
        s.TryGetValue(key, out var raw) && !string.IsNullOrEmpty(raw) ? raw : fallback;
}
