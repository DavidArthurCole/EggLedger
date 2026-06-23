using Microsoft.Data.Sqlite;

namespace EggLedger.Desktop.Storage;

/// <summary>
/// Owns one open <see cref="SqliteConnection"/> for a database file, applying the
/// pragmas the Go host uses (foreign_keys ON, WAL journal, busy_timeout) and the
/// embedded migrations for the named set. Mirrors Go db/init.go InitDB: open with
/// the pragma connection string, run migrations to the target version.
///
/// A single long-lived connection is held for the app lifetime. Microsoft.Data.
/// Sqlite serializes commands on one connection, matching the Go single-handle
/// model (DoDBOperation funnels through one *sql.DB). Tests pass a shared
/// in-memory data source so the schema survives between operations.
/// </summary>
public sealed class SqliteDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    private SqliteDatabase(SqliteConnection connection)
    {
        _connection = connection;
    }

    /// <summary>The open connection. Callers create commands against it directly.</summary>
    public SqliteConnection Connection => _connection;

    /// <summary>
    /// Opens (creating the parent dir) the mission DB at <paramref name="path"/>,
    /// applies pragmas, and migrates to schema v9.
    /// </summary>
    public static SqliteDatabase OpenMissionDb(string path)
    {
        var db = Open(path);
        SqliteMigrationRunner.MigrateMissionDb(db._connection);
        return db;
    }

    /// <summary>
    /// Opens (creating the parent dir) the report DB at <paramref name="path"/>,
    /// applies pragmas, and migrates to schema v12.
    /// </summary>
    public static SqliteDatabase OpenReportDb(string path)
    {
        var db = Open(path);
        SqliteMigrationRunner.MigrateReportDb(db._connection);
        return db;
    }

    /// <summary>
    /// Opens a connection at the raw data source string (a file path or
    /// <c>:memory:</c>) with the standard pragmas applied, without running any
    /// migrations. Used by tests and by the factory methods above.
    /// </summary>
    public static SqliteDatabase Open(string path)
    {
        if (path != ":memory:" && !path.StartsWith("Data Source", StringComparison.OrdinalIgnoreCase))
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        var builder = new SqliteConnectionStringBuilder
        {
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

    private static void ApplyPragmas(SqliteConnection connection, string path)
    {
        using var cmd = connection.CreateCommand();
        // WAL is unavailable for in-memory databases; skip it there but keep FKs and
        // busy_timeout to match the Go pragma string for file-backed DBs.
        var journal = path == ":memory:" ? "" : "PRAGMA journal_mode=WAL;";
        cmd.CommandText = $"PRAGMA foreign_keys=ON;{journal}PRAGMA busy_timeout=10000;";
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
