using System.Globalization;

namespace EggLedger.Web.Settings;

public sealed class SettingsModel {
    

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
    public const string KeyWindowWidth = "resolution_width";
    public const string KeyWindowHeight = "resolution_height";
    public const string KeyStartInFullscreen = "resolution_start_fullscreen";
    public const string KeyExportKeepCount = "export_keep_count";
    public const string KeyStorageFolderHidden = "storage_folder_hidden";
    public const string KeyBackupDestPath = "backup_dest_path";
    public const string KeyMoveDestPath = "move_dest_path";

    

    public const int MinWorkerCount = 1;
    public const int MaxWorkerCount = 10;

    public const int DefaultWindowWidth = 1280;
    public const int DefaultWindowHeight = 800;

    public bool AutoRefreshMenno { get; set; }

    public bool AutoRetry { get; set; } = true;

    public bool HideTimeoutErrors { get; set; }

    public int WorkerCount { get; set; } = MinWorkerCount;

    public bool ScreenshotSafety { get; set; }

    public bool ShowMissionProgress { get; set; } = true;

    public bool CollapseOlderSections { get; set; } = true;

    public bool AdvancedDropFilter { get; set; }

    public bool AutoExportCsv { get; set; } = true;

    public bool AutoExportXlsx { get; set; } = true;

    public bool WorkerCountWarningRead { get; set; }

    public int WindowWidth { get; set; }

    public int WindowHeight { get; set; }

    public bool StartInFullscreen { get; set; }

    public int ExportKeepCount { get; set; }

    public bool StorageFolderHidden { get; set; }

    public string BackupDestPath { get; set; } = "";

    public string MoveDestPath { get; set; } = "";

    public static int ClampWorkerCount(int n) => n < MinWorkerCount ? MinWorkerCount
        : n > MaxWorkerCount ? MaxWorkerCount
        : n;

    public void LoadFrom(IReadOnlyDictionary<string, string> settings) {
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
        WindowWidth = Int(settings, KeyWindowWidth, WindowWidth);
        WindowHeight = Int(settings, KeyWindowHeight, WindowHeight);
        StartInFullscreen = Bool(settings, KeyStartInFullscreen, StartInFullscreen);
        ExportKeepCount = Int(settings, KeyExportKeepCount, ExportKeepCount);
        StorageFolderHidden = Bool(settings, KeyStorageFolderHidden, StorageFolderHidden);
        BackupDestPath = Str(settings, KeyBackupDestPath, BackupDestPath);
        MoveDestPath = Str(settings, KeyMoveDestPath, MoveDestPath);
    }

    public static string FormatBool(bool v) => v ? "true" : "false";

    public static string FormatInt(int v) => v.ToString(CultureInfo.InvariantCulture);

    private static bool Bool(IReadOnlyDictionary<string, string> s, string key, bool fallback) =>
        s.TryGetValue(key, out var raw) && bool.TryParse(raw, out var v) ? v : fallback;

    private static int Int(IReadOnlyDictionary<string, string> s, string key, int fallback) =>
        s.TryGetValue(key, out var raw)
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v
            : fallback;

    private static string Str(IReadOnlyDictionary<string, string> s, string key, string fallback) =>
        s.TryGetValue(key, out var raw) ? raw : fallback;
}
