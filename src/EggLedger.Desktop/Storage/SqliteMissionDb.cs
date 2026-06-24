using EggLedger.Domain.Reports;
using Microsoft.Data.Sqlite;

namespace EggLedger.Desktop.Storage;

/// <summary>
/// Native <see cref="IMissionDb"/> backed by real SQLite: runs the parameterized
/// SQL from <see cref="ReportExecutor"/> against the mission / artifact_drops tables,
/// returning rows in the positional shape the executor consumes. Parity-tested
/// against the in-memory path. Row boxing: integers as long, REAL as double, text as
/// string.
/// </summary>
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
        cmd.CommandText = BindPositional(sql, args, cmd);

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

    // Rewrites "?" placeholders to named @aN params bound in order (the provider
    // won't bind bare "?"). Relies on QueryBuilder never emitting a literal "?";
    // a placeholder/arg count mismatch throws.
    private static string BindPositional(string sql, IReadOnlyList<object?> args, SqliteCommand cmd) {
        var result = sql;
        var idx = 0;
        while (true) {
            var pos = result.IndexOf('?', StringComparison.Ordinal);
            if (pos < 0) {
                break;
            }
            var name = "@a" + idx.ToString(System.Globalization.CultureInfo.InvariantCulture);
            result = result.Remove(pos, 1).Insert(pos, name);
            cmd.Parameters.AddWithValue(name, idx < args.Count ? args[idx] ?? DBNull.Value : DBNull.Value);
            idx++;
        }
        if (idx != args.Count) {
            throw new InvalidOperationException(
                $"placeholder/arg mismatch: {idx} '?' placeholders but {args.Count} args");
        }
        return result;
    }
}
