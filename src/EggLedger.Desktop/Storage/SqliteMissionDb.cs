using EggLedger.Domain.Reports;
using Microsoft.Data.Sqlite;

namespace EggLedger.Desktop.Storage;

public sealed class SqliteMissionDb : IMissionDb {
    private readonly SqliteConnection _connection;

    public SqliteMissionDb(SqliteDatabase missionDb) {
        ArgumentNullException.ThrowIfNull(missionDb);
        _connection = missionDb.Connection;
    }

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
        
        return reader.GetString(i);
    }
}
