using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace EggLedger.Desktop.Storage;

/// <summary>
/// Applies embedded SQL migrations to a SQLite database in numeric order and
/// tracks the applied version via <c>PRAGMA user_version</c>. Port of the Go
/// golang-migrate runner (db/migrate.go, reportdb/migrate.go): the same numbered
/// <c>N_name.up.sql</c> files run in order to a target version, and a re-run is a
/// no-op once <c>user_version</c> already equals the target.
///
/// Migrations are embedded resources named
/// <c>EggLedger.Desktop.Storage.Migrations.&lt;Set&gt;.&lt;N&gt;_&lt;name&gt;.up.sql</c>.
/// Each file's whole text runs inside one transaction so a partial failure rolls
/// back (matching golang-migrate's per-step transaction). PRAGMA user_version is
/// bumped to N after each successful file.
/// </summary>
public static class SqliteMigrationRunner
{
    /// <summary>Mission DB target schema version (Go db._schemaVersion = 9).</summary>
    public const int MissionTargetVersion = 9;

    /// <summary>Report DB target schema version (Go reportdb migrations 1..12).</summary>
    public const int ReportTargetVersion = 12;

    private static readonly Regex FileNamePattern =
        new(@"\.Migrations\.(?<set>[^.]+)\.(?<num>\d+)_", RegexOptions.Compiled);

    /// <summary>
    /// Applies every mission-set migration up to <see cref="MissionTargetVersion"/>.
    /// Idempotent: a DB already at the target version is left untouched.
    /// </summary>
    public static void MigrateMissionDb(SqliteConnection connection) =>
        Migrate(connection, "Mission", MissionTargetVersion);

    /// <summary>
    /// Applies every report-set migration up to <see cref="ReportTargetVersion"/>.
    /// Idempotent.
    /// </summary>
    public static void MigrateReportDb(SqliteConnection connection) =>
        Migrate(connection, "Report", ReportTargetVersion);

    /// <summary>
    /// Runs the named migration set against <paramref name="connection"/>, applying
    /// only files numbered above the current <c>user_version</c> and up to
    /// <paramref name="targetVersion"/>, in ascending numeric order.
    /// </summary>
    public static void Migrate(SqliteConnection connection, string set, int targetVersion)
    {
        var migrations = LoadMigrations(set);
        int current = GetUserVersion(connection);

        foreach (var (version, sql) in migrations)
        {
            if (version <= current || version > targetVersion)
            {
                continue;
            }
            ApplyOne(connection, version, sql);
            current = version;
        }
    }

    private static void ApplyOne(SqliteConnection connection, int version, string sql)
    {
        using var tx = connection.BeginTransaction();
        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
        // user_version is a PRAGMA; it cannot be parameterized and is set after the
        // migration body so a failed body rolls the whole step back.
        using (var setVersion = connection.CreateCommand())
        {
            setVersion.Transaction = tx;
            setVersion.CommandText = string.Format(
                CultureInfo.InvariantCulture, "PRAGMA user_version = {0};", version);
            setVersion.ExecuteNonQuery();
        }
        tx.Commit();
    }

    private static int GetUserVersion(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        var result = cmd.ExecuteScalar();
        return result is null ? 0 : Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private static List<(int Version, string Sql)> LoadMigrations(string set)
    {
        var assembly = typeof(SqliteMigrationRunner).Assembly;
        var prefix = $".Migrations.{set}.";
        var result = new List<(int, string)>();

        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.Contains(prefix, StringComparison.Ordinal) || !name.EndsWith(".up.sql", StringComparison.Ordinal))
            {
                continue;
            }
            var match = FileNamePattern.Match(name);
            if (!match.Success || !string.Equals(match.Groups["set"].Value, set, StringComparison.Ordinal))
            {
                continue;
            }
            int version = int.Parse(match.Groups["num"].Value, CultureInfo.InvariantCulture);
            result.Add((version, ReadResource(assembly, name)));
        }

        result.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        return result;
    }

    private static string ReadResource(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"missing embedded migration {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
