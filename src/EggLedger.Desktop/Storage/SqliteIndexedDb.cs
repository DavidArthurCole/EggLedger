using System.Globalization;
using System.Text.Json;
using EggLedger.Web.Data;
using Microsoft.Data.Sqlite;

namespace EggLedger.Desktop.Storage;

/// <summary>
/// Native SQLite implementation of <see cref="IIndexedDb"/> (the browser uses a
/// JS-interop wrapper). Each logical store maps to a table whose columns are the
/// snake_case JSON property names of the row record; rows round-trip through a
/// <see cref="JsonElement"/>. Operations dispatch to the mission or report DB by
/// store name.
/// </summary>
public sealed class SqliteIndexedDb : IIndexedDb {
    private readonly SqliteDatabase _missionDb;
    private readonly SqliteDatabase _reportDb;
    private readonly Dictionary<string, StoreMeta> _stores;

    /// <param name="missionDb">DB holding mission/backup/artifact_drops/settings.</param>
    /// <param name="reportDb">DB holding reports/report_groups.</param>
    public SqliteIndexedDb(SqliteDatabase missionDb, SqliteDatabase reportDb) {
        _missionDb = missionDb ?? throw new ArgumentNullException(nameof(missionDb));
        _reportDb = reportDb ?? throw new ArgumentNullException(nameof(reportDb));
        _stores = BuildStoreMeta();
        BindBlobColumns();
    }

    private static readonly JsonSerializerOptions JsonOpts = Rows.JsonOptions;

    public ValueTask PutAsync(string store, object value) {
        var meta = Meta(store);
        Upsert(meta, value);
        return ValueTask.CompletedTask;
    }

    public ValueTask<int> PutManyAsync(string store, IEnumerable<object> values) {
        var meta = Meta(store);
        var connection = Conn(meta);
        using var tx = connection.BeginTransaction();
        int count = 0;
        foreach (var value in values) {
            Upsert(meta, value, tx);
            count++;
        }
        tx.Commit();
        return ValueTask.FromResult(count);
    }

    public ValueTask<T?> GetAsync<T>(string store, object key) {
        var meta = Meta(store);
        var connection = Conn(meta);
        using var cmd = connection.CreateCommand();
        var (where, keyArgs) = KeyPredicate(meta, key);
        cmd.CommandText = $"SELECT * FROM {meta.Table} WHERE {where} LIMIT 1;";
        BindArgs(cmd, keyArgs);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) {
            return ValueTask.FromResult<T?>(default);
        }
        return ValueTask.FromResult<T?>(Materialize<T>(meta, reader));
    }

    public ValueTask<T[]> GetAllAsync<T>(string store) {
        var meta = Meta(store);
        var connection = Conn(meta);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT * FROM {meta.Table};";
        return ValueTask.FromResult(ReadAll<T>(meta, cmd));
    }

    public ValueTask<T[]> GetAllByIndexAsync<T>(string store, string index, object value) {
        var meta = Meta(store);
        var connection = Conn(meta);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT * FROM {meta.Table} WHERE {index} = ?;";
        BindArgs(cmd, [value]);
        return ValueTask.FromResult(ReadAll<T>(meta, cmd));
    }

    public ValueTask DeleteAsync(string store, object key) {
        var meta = Meta(store);
        var connection = Conn(meta);
        using var cmd = connection.CreateCommand();
        var (where, keyArgs) = KeyPredicate(meta, key);
        cmd.CommandText = $"DELETE FROM {meta.Table} WHERE {where};";
        BindArgs(cmd, keyArgs);
        cmd.ExecuteNonQuery();
        return ValueTask.CompletedTask;
    }

    public ValueTask ClearAsync(string store) {
        var meta = Meta(store);
        var connection = Conn(meta);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"DELETE FROM {meta.Table};";
        cmd.ExecuteNonQuery();
        return ValueTask.CompletedTask;
    }

    public ValueTask<int> CountAsync(string store) {
        var meta = Meta(store);
        var connection = Conn(meta);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {meta.Table};";
        var result = cmd.ExecuteScalar();
        return ValueTask.FromResult(result is null ? 0 : Convert.ToInt32(result, CultureInfo.InvariantCulture));
    }

    private void Upsert(StoreMeta meta, object value, SqliteTransaction? tx = null) {
        // Serialize to JSON; snake_case property names are the column names. A null
        // autoIncrement column is skipped so SQLite assigns it.
        using var doc = JsonSerializer.SerializeToDocument(value, value.GetType(), JsonOpts);
        var props = new List<(string Col, JsonElement Val)>();
        foreach (var prop in doc.RootElement.EnumerateObject()) {
            if (meta.AutoIncrementColumn == prop.Name && prop.Value.ValueKind is JsonValueKind.Null) {
                continue;
            }
            props.Add((prop.Name, prop.Value));
        }

        var connection = Conn(meta);
        using var cmd = connection.CreateCommand();
        if (tx is not null) {
            cmd.Transaction = tx;
        }

        var cols = props.Select(p => p.Col).ToList();
        var placeholders = props.Select((_, i) => "@p" + i.ToString(CultureInfo.InvariantCulture)).ToList();

        if (meta.UpsertByDelete) {
            // No UNIQUE(player_id) on the backup table, so ON CONFLICT is impossible.
            // Emulate keyPath=player_id (one row per player) by delete-then-insert.
            DeleteByKey(meta, props, connection, tx);
        }

        string conflict;
        if (meta.AutoIncrementColumn is not null || meta.UpsertByDelete) {
            // Plain INSERT: autoIncrement stores get a fresh key; UpsertByDelete
            // stores already removed any prior row above.
            conflict = "";
        } else {
            var updates = cols
                .Where(c => !meta.KeyColumns.Contains(c))
                .Select(c => $"{c} = excluded.{c}");
            conflict = $" ON CONFLICT({string.Join(", ", meta.KeyColumns)}) DO UPDATE SET {string.Join(", ", updates)}";
        }

        cmd.CommandText =
            $"INSERT INTO {meta.Table} ({string.Join(", ", cols)}) VALUES ({string.Join(", ", placeholders)}){conflict};";
        for (var i = 0; i < props.Count; i++) {
            cmd.Parameters.AddWithValue(placeholders[i], JsonToDbValue(props[i].Val, meta.BlobColumns.Contains(props[i].Col)));
        }
        cmd.ExecuteNonQuery();
    }

    // Deletes any rows matching the row's key-column values. Used by UpsertByDelete
    // stores to emulate put-by-keyPath against a table with no UNIQUE on the key.
    private static void DeleteByKey(
        StoreMeta meta, List<(string Col, JsonElement Val)> props, SqliteConnection connection, SqliteTransaction? tx) {
        using var del = connection.CreateCommand();
        if (tx is not null) {
            del.Transaction = tx;
        }
        var clauses = new List<string>(meta.KeyColumns.Length);
        var k = 0;
        foreach (var keyCol in meta.KeyColumns) {
            var match = props.First(p => p.Col == keyCol);
            var name = "@d" + k.ToString(CultureInfo.InvariantCulture);
            clauses.Add($"{keyCol} = {name}");
            del.Parameters.AddWithValue(name, JsonToDbValue(match.Val, meta.BlobColumns.Contains(keyCol)));
            k++;
        }
        del.CommandText = $"DELETE FROM {meta.Table} WHERE {string.Join(" AND ", clauses)};";
        del.ExecuteNonQuery();
    }

    private static T[] ReadAll<T>(StoreMeta meta, SqliteCommand cmd) {
        var result = new List<T>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) {
            result.Add(Materialize<T>(meta, reader));
        }
        return [.. result];
    }

    // Rebuilds the row's JSON object from the SELECT columns, then deserializes to
    // T. Column names are the JSON property names, so the round-trip is exact.
    private static T Materialize<T>(StoreMeta meta, SqliteDataReader reader) {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer)) {
            writer.WriteStartObject();
            for (var i = 0; i < reader.FieldCount; i++) {
                var name = reader.GetName(i);
                writer.WritePropertyName(name);
                WriteColumn(writer, reader, i, meta.BoolColumns.Contains(name), meta.BlobColumns.Contains(name));
            }
            writer.WriteEndObject();
        }
        var json = buffer.ToArray();
        return JsonSerializer.Deserialize<T>(json, JsonOpts)
            ?? throw new InvalidOperationException($"failed to materialize row for {meta.Table}");
    }

    private static void WriteColumn(Utf8JsonWriter writer, SqliteDataReader reader, int i, bool isBool, bool isBlob) {
        if (reader.IsDBNull(i)) {
            writer.WriteNullValue();
            return;
        }
        if (isBlob) {
            // byte[] columns are base64 on the JSON wire (matches System.Text.Json).
            var bytes = (byte[])reader.GetValue(i);
            writer.WriteBase64StringValue(bytes);
            return;
        }
        if (isBool) {
            writer.WriteBooleanValue(reader.GetInt64(i) != 0);
            return;
        }
        switch (reader.GetFieldType(i)) {
            case var t when t == typeof(long):
                writer.WriteNumberValue(reader.GetInt64(i));
                break;
            case var t when t == typeof(double):
                writer.WriteNumberValue(reader.GetDouble(i));
                break;
            case var t when t == typeof(byte[]):
                writer.WriteBase64StringValue((byte[])reader.GetValue(i));
                break;
            default:
                writer.WriteStringValue(reader.GetString(i));
                break;
        }
    }

    private static object JsonToDbValue(JsonElement el, bool isBlob) => el.ValueKind switch {
        JsonValueKind.Null => DBNull.Value,
        JsonValueKind.True => 1L,
        JsonValueKind.False => 0L,
        JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
        JsonValueKind.String => DecodeString(el, isBlob),
        _ => el.GetRawText(),
    };

    // System.Text.Json serializes byte[] columns as base64 strings. Blob columns
    // must be stored as real BLOBs (so they read back as byte[]); plain text columns
    // stay strings.
    private static object DecodeString(JsonElement el, bool isBlob) {
        var s = el.GetString() ?? "";
        return isBlob ? Convert.FromBase64String(s) : s;
    }

    private SqliteConnection Conn(StoreMeta meta) =>
        meta.UseReportDb ? _reportDb.Connection : _missionDb.Connection;

    private StoreMeta Meta(string store) =>
        _stores.TryGetValue(store, out var meta)
            ? meta
            : throw new ArgumentException($"unknown store {store}", nameof(store));

    private static (string where, object[] args) KeyPredicate(StoreMeta meta, object key) {
        if (meta.KeyColumns.Length == 1) {
            return ($"{meta.KeyColumns[0]} = ?", new[] { key });
        }
        // Composite key (mission: [player_id, mission_id]) arrives as an array.
        if (key is object[] parts && parts.Length == meta.KeyColumns.Length) {
            var where = string.Join(" AND ", meta.KeyColumns.Select(c => $"{c} = ?"));
            return (where, parts);
        }
        throw new ArgumentException($"composite key expected for store {meta.Table}", nameof(key));
    }

    private static void BindArgs(SqliteCommand cmd, object[] args) {
        // IIndexedDb passes positional "?" args; bind them in order.
        for (var i = 0; i < args.Length; i++) {
            cmd.Parameters.AddWithValue("@k" + i.ToString(CultureInfo.InvariantCulture), args[i] ?? DBNull.Value);
        }
        // Rewrite "?" placeholders to the named parameters Microsoft.Data.Sqlite binds.
        var sql = cmd.CommandText;
        var idx = 0;
        while (sql.Contains('?')) {
            var pos = sql.IndexOf('?');
            sql = sql.Remove(pos, 1).Insert(pos, "@k" + idx.ToString(CultureInfo.InvariantCulture));
            idx++;
        }
        if (idx != args.Length) {
            throw new InvalidOperationException(
                $"placeholder/arg mismatch: {idx} '?' placeholders but {args.Length} args");
        }
        cmd.CommandText = sql;
    }

    private static Dictionary<string, StoreMeta> BuildStoreMeta() => new(StringComparer.Ordinal) {
        ["mission"] = new StoreMeta(
            "mission", useReportDb: false,
            keyColumns: ["player_id", "mission_id"],
            autoIncrementColumn: null,
            boolColumns: ["is_dub_cap", "is_bugged_cap"]),
        ["backup"] = new StoreMeta(
            "backup", useReportDb: false,
            keyColumns: ["player_id"],
            autoIncrementColumn: null,
            boolColumns: [],
            // No UNIQUE(player_id), so emulate keyPath=player_id by delete-then-insert.
            upsertByDelete: true),
        ["artifact_drops"] = new StoreMeta(
            "artifact_drops", useReportDb: false,
            keyColumns: ["id"],
            autoIncrementColumn: "id",
            boolColumns: []),
        ["settings"] = new StoreMeta(
            "settings", useReportDb: false,
            keyColumns: ["key"],
            autoIncrementColumn: null,
            boolColumns: []),
        ["reports"] = new StoreMeta(
            "reports", useReportDb: true,
            keyColumns: ["id"],
            autoIncrementColumn: null,
            boolColumns: ["menno_enabled"]),
        ["report_groups"] = new StoreMeta(
            "report_groups", useReportDb: true,
            keyColumns: ["id"],
            autoIncrementColumn: null,
            boolColumns: []),
    };

    // Derives BLOB columns from PRAGMA table_info (schema is the source of truth) so
    // a new BLOB column never silently stores as base64 TEXT.
    private void BindBlobColumns() {
        foreach (var meta in _stores.Values) {
            var connection = Conn(meta);
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info({meta.Table});";
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) {
                // table_info columns: cid, name, type, notnull, dflt_value, pk.
                var name = reader.GetString(1);
                var declaredType = reader.IsDBNull(2) ? "" : reader.GetString(2);
                if (declaredType.Equals("BLOB", StringComparison.OrdinalIgnoreCase)) {
                    meta.BlobColumns.Add(name);
                }
            }
        }
    }

    private sealed class StoreMeta {
        public StoreMeta(
            string table, bool useReportDb, string[] keyColumns, string? autoIncrementColumn,
            string[] boolColumns, bool upsertByDelete = false) {
            Table = table;
            UseReportDb = useReportDb;
            KeyColumns = keyColumns;
            AutoIncrementColumn = autoIncrementColumn;
            BoolColumns = new HashSet<string>(boolColumns, StringComparer.Ordinal);
            BlobColumns = new HashSet<string>(StringComparer.Ordinal);
            UpsertByDelete = upsertByDelete;
        }

        public string Table { get; }
        public bool UseReportDb { get; }
        public string[] KeyColumns { get; }
        public string? AutoIncrementColumn { get; }
        public bool UpsertByDelete { get; }
        public HashSet<string> BoolColumns { get; }
        public HashSet<string> BlobColumns { get; }
    }
}
