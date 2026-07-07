using EggLedger.Domain.Reports;
using Microsoft.Data.Sqlite;

namespace EggLedger.Desktop.Storage;

/// <summary>Native <see cref="IMissionDb"/> backed by real SQLite, parity-tested against the in-memory path.</summary>
/// <remarks>
/// Runs the parameterized SQL from <see cref="ReportExecutor"/> against the mission /
/// artifact_drops tables, returning rows in the positional shape the executor
/// consumes. Row boxing: integers as long, REAL as double, text as string.
/// </remarks>
public sealed class SqliteMissionDb : IMissionDb {
    private readonly SqliteConnection _connection;

    public SqliteMissionDb(SqliteDatabase missionDb) {
        ArgumentNullException.ThrowIfNull(missionDb);
        _connection = missionDb.Connection;
    }

    /// <summary>Direct-connection constructor for tests.</summary>
    public SqliteMissionDb(SqliteConnection connection) {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public IReadOnlyList<object?[]> Query(string sql, IReadOnlyList<object?> args) {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = SqlPlaceholderBinder.Rewrite(sql, args, cmd, "a");

        var rows = new List<object?[]>();
        using var reader = cmd.ExecuteReader();
        int columnCount = reader.FieldCount;
        while (reader.Read()) {
            var row = new object?[columnCount];
            for (var i = 0; i < columnCount; i++) {
                row[i] = ReadValue(reader, i);
            }
            rows.Add(row);
        }
        return rows;
    }

    // Boxes a column the way the executor expects: integers as long, reals as
    // double, text as string, NULL as null.
    private static object? ReadValue(SqliteDataReader reader, int i) {
        if (reader.IsDBNull(i)) {
            return null;
        }
        var type = reader.GetFieldType(i);
        if (type == typeof(long)) {
            return reader.GetInt64(i);
        }
        if (type == typeof(double)) {
            return reader.GetDouble(i);
        }
        if (type == typeof(byte[])) {
            return reader.GetValue(i);
        }
        // CAST(... AS TEXT) and strftime produce text; COUNT(*) is integer affinity.
        return reader.GetString(i);
    }
}
