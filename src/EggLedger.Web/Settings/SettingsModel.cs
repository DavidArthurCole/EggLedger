using System.Globalization;

namespace EggLedger.Web.Settings;

/// <summary>Settings-tab model: defaults, Go setting keys, and (de)serialization. Values persist as string key/value via <c>IndexedDbSettings</c> using the Go keys so behavior matches across builds.</summary>
public sealed class SettingsModel
{
    // Setting keys (match the Go storage.go dbSet calls).

    public const string KeyAutoRefreshMenno = "auto_refresh_menno_pref";
    public const string KeyRetryFailedMissions = "retry_failed_missions";
    public const string KeyHideTimeoutErrors = "hide_timeout_errors";
    public const string KeyWorkerCount = "worker_count";
    public const string KeyScreenshotSafety = "screenshot_safety";
    public const string KeyShowMissionProgress = "show_mission_progress";
    public const string KeyCollapseOlderSections = "collapse_older_sections";
    public const string KeyAdvancedDropFilter = "advanced_drop_filter";
    public const string KeyAutoExportCsv = "auto_export_csv";
    public const string KeyAutoExportXlsx = "auto_export_xlsx";
    public const string KeyWorkerCountWarningRead = "worker_count_warning_read";

    // Worker-count bounds (clamped on read).

    public const int MinWorkerCount = 1;
    public const int MaxWorkerCount = 10;

    // Settings-tab fields with their defaults.

    /// <summary>Refresh Menno community data once weekly. Default false.</summary>
    public bool AutoRefreshMenno { get; set; }

    /// <summary>Auto-retry missions that fail during a fetch. Default false.</summary>
    public bool AutoRetry { get; set; }

    /// <summary>Hide per-mission timeout errors in the fetch log. Default false.</summary>
    public bool HideTimeoutErrors { get; set; }

    /// <summary>Parallel download workers, 1..10. Go default 1 (clamped on read).</summary>
    public int WorkerCount { get; set; } = MinWorkerCount;

    /// <summary>Mask player EIDs on screen. Default false.</summary>
    public bool ScreenshotSafety { get; set; }

    /// <summary>Show individual mission progress while fetching. Go default true.</summary>
    public bool ShowMissionProgress { get; set; } = true;

    /// <summary>Collapse all but the most recent year section. Go default true.</summary>
    public bool CollapseOlderSections { get; set; } = true;

    /// <summary>Advanced drop filter (machine-local preference). Default false.</summary>
    public bool AdvancedDropFilter { get; set; }

    /// <summary>Auto-export CSV on fetch. Go default true (on for existing installs).</summary>
    public bool AutoExportCsv { get; set; } = true;

    /// <summary>Auto-export XLSX on fetch. Go default true (on for existing installs).</summary>
    public bool AutoExportXlsx { get; set; } = true;

    /// <summary>Whether the worker-count warning has been dismissed. Default false.</summary>
    public bool WorkerCountWarningRead { get; set; }

    /// <summary>Clamps a worker count to [1, 10].</summary>
    public static int ClampWorkerCount(int n) => n < MinWorkerCount ? MinWorkerCount
        : n > MaxWorkerCount ? MaxWorkerCount
        : n;

    /// <summary>Hydrates the model from a settings map. Missing keys keep the Go defaults; worker count is clamped to [1, 10].</summary>
    public void LoadFrom(IReadOnlyDictionary<string, string> settings)
    {
        AutoRefreshMenno = Bool(settings, KeyAutoRefreshMenno, AutoRefreshMenno);
        AutoRetry = Bool(settings, KeyRetryFailedMissions, AutoRetry);
        HideTimeoutErrors = Bool(settings, KeyHideTimeoutErrors, HideTimeoutErrors);
        WorkerCount = ClampWorkerCount(Int(settings, KeyWorkerCount, WorkerCount));
        ScreenshotSafety = Bool(settings, KeyScreenshotSafety, ScreenshotSafety);
        ShowMissionProgress = Bool(settings, KeyShowMissionProgress, ShowMissionProgress);
        CollapseOlderSections = Bool(settings, KeyCollapseOlderSections, CollapseOlderSections);
        AdvancedDropFilter = Bool(settings, KeyAdvancedDropFilter, AdvancedDropFilter);
        AutoExportCsv = Bool(settings, KeyAutoExportCsv, AutoExportCsv);
        AutoExportXlsx = Bool(settings, KeyAutoExportXlsx, AutoExportXlsx);
        WorkerCountWarningRead = Bool(settings, KeyWorkerCountWarningRead, WorkerCountWarningRead);
    }

    /// <summary>Serializes a bool the way Go strconv.FormatBool does ("true"/"false").</summary>
    public static string FormatBool(bool v) => v ? "true" : "false";

    /// <summary>Serializes an int the way Go strconv.Itoa does (invariant base-10).</summary>
    public static string FormatInt(int v) => v.ToString(CultureInfo.InvariantCulture);

    private static bool Bool(IReadOnlyDictionary<string, string> s, string key, bool fallback) =>
        s.TryGetValue(key, out var raw) && bool.TryParse(raw, out var v) ? v : fallback;

    private static int Int(IReadOnlyDictionary<string, string> s, string key, int fallback) =>
        s.TryGetValue(key, out var raw)
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v
            : fallback;
}
