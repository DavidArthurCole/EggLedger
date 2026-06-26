namespace EggLedger.Web.Data;

/// <summary>
/// Canonical IndexedDB store and index names. The string values are the wire/schema
/// contract shared by every <see cref="IIndexedDb"/> impl (browser, Postgres, SQLite);
/// do not change them.
/// </summary>
public static class IndexedDbStores {
    public const string Mission = "mission";
    public const string Backup = "backup";
    public const string ArtifactDrops = "artifact_drops";
    public const string Settings = "settings";
    public const string Reports = "reports";
    public const string ReportGroups = "report_groups";
    public const string PlayerIdIndex = "player_id";
    public const string AccountIdIndex = "account_id";
}
