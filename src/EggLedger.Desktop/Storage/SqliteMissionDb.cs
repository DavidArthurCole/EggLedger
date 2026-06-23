using Microsoft.Data.Sqlite;
using EggLedger.Domain.Reports;

namespace EggLedger.Desktop.Storage;

/// <summary>
/// Native <see cref="IMissionDb"/> backed by real SQLite. This is where the SQL
/// report path goes live on the desktop: <see cref="ReportExecutor"/> emits the
/// parameterized SQL built by <see cref="QueryBuilder"/> and this executes it
/// against the <c>mission</c> / <c>artifact_drops</c> tables, returning rows in
/// the exact positional shape the executor consumes.
///
/// The Go reference ran the same SQL through its injected <c>*sql.DB</c>
/// (reports.SetMissionDB); the C# in-memory path (<see cref="InMemoryMissionDb"/>)
/// emulates the same query shapes over typed rows. This implementation closes the
/// loop by running the SQL natively, and is parity-tested against the in-memory
/// path to guarantee identical output.
///
/// Row boxing matches the <see cref="IMissionDb"/> contract: integer columns as
/// <see cref="long"/>, REAL columns as <see cref="double"/>, text as
/// <see cref="string"/> (the executor's AsString/AsLong/AsDouble accept all).
/// </summary>
public sealed class SqliteMissionDb : IMissionDb
{
    private readonly SqliteConnection _connection;

    public SqliteMissionDb(SqliteDatabase missionDb)
    {
        ArgumentNullException.ThrowIfNull(missionDb);
        _connection = missionDb.Connection;
    }

    /// <summary>Direct-connection constructor for tests.</summary>
    public SqliteMissionDb(SqliteConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public IReadOnlyList<object?[]> Query(string sql, IReadOnlyList<object?> args)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = BindPositional(sql, args, cmd);

        var rows = new List<object?[]>();
        using var reader = cmd.ExecuteReader();
        int columnCount = reader.FieldCount;
        while (reader.Read())
        {
            var row = new object?[columnCount];
            for (var i = 0; i < columnCount; i++)
            {
                row[i] = ReadValue(reader, i);
            }
            rows.Add(row);
        }
        return rows;
    }

    // Boxes a column the way the executor expects: integers as long, reals as
    // double, text as string, NULL as null.
    private static object? ReadValue(SqliteDataReader reader, int i)
    {
        if (reader.IsDBNull(i))
        {
            return null;
        }
        var type = reader.GetFieldType(i);
        if (type == typeof(long))
        {
            return reader.GetInt64(i);
        }
        if (type == typeof(double))
        {
            return reader.GetDouble(i);
        }
        if (type == typeof(byte[]))
        {
            return reader.GetValue(i);
        }
        // CAST(... AS TEXT) and strftime produce text; COUNT(*) is integer affinity.
        return reader.GetString(i);
    }

    // Rewrites the "?" positional placeholders QueryBuilder emits into named
    // parameters and binds args in order. SQLite's provider does not bind bare "?"
    // positionally, so we substitute @aN left-to-right. This relies on QueryBuilder
    // never emitting a literal "?" in SQL text; the placeholder count must equal
    // args.Count or the query is malformed, so a mismatch throws.
    private static string BindPositional(string sql, IReadOnlyList<object?> args, SqliteCommand cmd)
    {
        var result = sql;
        var idx = 0;
        while (true)
        {
            var pos = result.IndexOf('?', StringComparison.Ordinal);
            if (pos < 0)
            {
                break;
            }
            var name = "@a" + idx.ToString(System.Globalization.CultureInfo.InvariantCulture);
            result = result.Remove(pos, 1).Insert(pos, name);
            cmd.Parameters.AddWithValue(name, idx < args.Count ? args[idx] ?? DBNull.Value : DBNull.Value);
            idx++;
        }
        if (idx != args.Count)
        {
            throw new InvalidOperationException(
                $"placeholder/arg mismatch: {idx} '?' placeholders but {args.Count} args");
        }
        return result;
    }
}
