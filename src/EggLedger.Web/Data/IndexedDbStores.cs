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

    private static readonly HashSet<string> Indexes = [PlayerIdIndex, AccountIdIndex];

    /// <summary>Guards the one caller-influenced SQL identifier: the index column must be a known
    /// name. Belt-and-suspenders over the per-impl identifier quoting.</summary>
    public static string ValidIndex(string index) =>
        Indexes.Contains(index) ? index
            : throw new ArgumentException($"unknown index '{index}'", nameof(index));
}
