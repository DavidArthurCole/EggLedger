using Microsoft.Data.Sqlite;

namespace EggLedger.Desktop.Storage;

/// <summary>Owns one long-lived <see cref="SqliteConnection"/> per database file.</summary>
/// <remarks>
/// Microsoft.Data.Sqlite serializes commands on the held connection, matching the Go
/// single-handle model. Tests pass a shared in-memory data source so the schema
/// survives between operations.
/// </remarks>
public sealed class SqliteDatabase : IDisposable {
    private SqliteDatabase(SqliteConnection connection) {
        Connection = connection;
    }

    public SqliteConnection Connection { get; }

    /// <summary>Opens the mission DB at <paramref name="path"/> and migrates to schema v9.</summary>
    public static SqliteDatabase OpenMissionDb(string path) {
        var db = Open(path);
        SqliteMigrationRunner.MigrateMissionDb(db.Connection);
        return db;
    }

    /// <summary>Opens the report DB at <paramref name="path"/> and migrates to schema v12.</summary>
    public static SqliteDatabase OpenReportDb(string path) {
        var db = Open(path);
        SqliteMigrationRunner.MigrateReportDb(db.Connection);
        return db;
    }

    /// <summary>
    /// Opens a connection at the raw data source (file path or <c>:memory:</c>) with
    /// standard pragmas, no migrations. Used by tests and the factory methods above.
    /// </summary>
    public static SqliteDatabase Open(string path) {
        if (path != ":memory:" && !path.StartsWith("Data Source", StringComparison.OrdinalIgnoreCase)) {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) {
                Directory.CreateDirectory(dir);
            }
        }

        var builder = new SqliteConnectionStringBuilder {
            DataSource = path,
            // Shared cache keeps an in-memory ":memory:" database alive across the
            // single held connection (and any reuse) for the process lifetime.
            Cache = path == ":memory:" ? SqliteCacheMode.Shared : SqliteCacheMode.Default,
        };

        var connection = new SqliteConnection(builder.ConnectionString);
        connection.Open();
        ApplyPragmas(connection, path);
        return new SqliteDatabase(connection);
    }

    private static void ApplyPragmas(SqliteConnection connection, string path) {
        using var cmd = connection.CreateCommand();
        // WAL is unavailable for in-memory databases; skip it there but keep FKs and
        // busy_timeout to match the Go pragma string for file-backed DBs.
        var journal = path == ":memory:" ? "" : "PRAGMA journal_mode=WAL;";
        cmd.CommandText = $"PRAGMA foreign_keys=ON;{journal}PRAGMA busy_timeout=10000;";
        cmd.ExecuteNonQuery();
    }

    public void Dispose() {
        Connection.Dispose();
    }
}
