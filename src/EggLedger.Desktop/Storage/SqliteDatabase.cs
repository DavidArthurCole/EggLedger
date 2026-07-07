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
        var db = Open(path, isConnectionString: false);
        SqliteMigrationRunner.MigrateMissionDb(db.Connection);
        return db;
    }

    /// <summary>Opens the report DB at <paramref name="path"/> and migrates to schema v12.</summary>
    public static SqliteDatabase OpenReportDb(string path) {
        var db = Open(path, isConnectionString: false);
        SqliteMigrationRunner.MigrateReportDb(db.Connection);
        return db;
    }

    /// <summary>
    /// Opens a connection at <paramref name="path"/> with standard pragmas, no migrations.
    /// Used by tests and the factory methods above.
    /// </summary>
    /// <param name="path">A bare file path (or <c>:memory:</c>), unless <paramref name="isConnectionString"/> is set.</param>
    /// <param name="isConnectionString">True when <paramref name="path"/> is already a full Sqlite connection string (e.g. a shared-cache in-memory test DB) rather than a bare path.</param>
    public static SqliteDatabase Open(string path, bool isConnectionString = false) {
        if (!isConnectionString && path != ":memory:") {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) {
                Directory.CreateDirectory(dir);
            }
        }

        string connectionString;
        if (isConnectionString) {
            connectionString = path;
        } else {
            var builder = new SqliteConnectionStringBuilder {
                DataSource = path,
                // Shared cache keeps an in-memory ":memory:" database alive across the
                // single held connection (and any reuse) for the process lifetime.
                Cache = path == ":memory:" ? SqliteCacheMode.Shared : SqliteCacheMode.Default,
            };
            connectionString = builder.ConnectionString;
        }

        var connection = new SqliteConnection(connectionString);
        connection.Open();
        ApplyPragmas(connection, path, isConnectionString);
        return new SqliteDatabase(connection);
    }

    private static void ApplyPragmas(SqliteConnection connection, string path, bool isConnectionString) {
        using var cmd = connection.CreateCommand();
        // WAL is unavailable for in-memory databases; skip it there but keep FKs and
        // busy_timeout to match the Go pragma string for file-backed DBs.
        var isMemory = isConnectionString
            ? path.Contains("Mode=Memory", StringComparison.OrdinalIgnoreCase)
            : path == ":memory:";
        var journal = isMemory ? "" : "PRAGMA journal_mode=WAL;";
        cmd.CommandText = $"PRAGMA foreign_keys=ON;{journal}PRAGMA busy_timeout=10000;";
        cmd.ExecuteNonQuery();
    }

    public void Dispose() {
        Connection.Dispose();
    }
}
