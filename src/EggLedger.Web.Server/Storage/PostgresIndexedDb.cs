using System.Globalization;
using System.Text.Json;
using EggLedger.Web.Data;
using Npgsql;
using NpgsqlTypes;

namespace EggLedger.Web.Server.Storage;

/// <summary>
/// Postgres implementation of <see cref="IIndexedDb"/> for the Blazor Server host,
/// scoped to one Discord user. Each logical store maps to an <c>el_*</c> table whose
/// columns are the snake_case JSON property names of the row record; rows round-trip
/// through a <see cref="JsonElement"/> (same contract as the desktop SqliteIndexedDb).
///
/// Multi-tenant: EVERY query is scoped by <c>discord_id = @user</c> and every insert
/// carries it. The scope is injected centrally here (one code path) so a row can never
/// cross users. Registered scoped per circuit; the user id is read per op from
/// <see cref="CurrentUser"/> (the cookie-auth principal flowed into the circuit).
/// </summary>
public sealed class PostgresIndexedDb : IIndexedDb {
    private readonly NpgsqlDataSource _source;
    private readonly CurrentUser _user;
    private readonly Dictionary<string, StoreMeta> _stores;

    public PostgresIndexedDb(NpgsqlDataSource source, CurrentUser user) {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _user = user ?? throw new ArgumentNullException(nameof(user));
        _stores = BuildStoreMeta();
    }

    // Resolved per op from the circuit's auth principal.
    private Task<string> UserAsync() => _user.RequireAsync();

    private static readonly JsonSerializerOptions JsonOpts = Rows.JsonOptions;

    public async ValueTask PutAsync(string store, object value) {
        await using var conn = await _source.OpenConnectionAsync().ConfigureAwait(false);
        await UpsertAsync(Meta(store), value, conn, tx: null).ConfigureAwait(false);
    }

    public async ValueTask<int> PutManyAsync(string store, IEnumerable<object> values) {
        var meta = Meta(store);
        await using var conn = await _source.OpenConnectionAsync().ConfigureAwait(false);
        await using var tx = await conn.BeginTransactionAsync().ConfigureAwait(false);
        int count = 0;
        foreach (var value in values) {
            await UpsertAsync(meta, value, conn, tx).ConfigureAwait(false);
            count++;
        }
        await tx.CommitAsync().ConfigureAwait(false);
        return count;
    }

    public async ValueTask<T?> GetAsync<T>(string store, object key) {
        var meta = Meta(store);
        var (where, keyArgs) = KeyPredicate(meta, key);
        await using var conn = await _source.OpenConnectionAsync().ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT * FROM {meta.Table} WHERE discord_id = @user AND {where} LIMIT 1;";
        cmd.Parameters.AddWithValue("user", await UserAsync());
        BindKeyArgs(cmd, keyArgs);
        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        if (!await reader.ReadAsync().ConfigureAwait(false)) {
            return default;
        }
        return Materialize<T>(meta, reader);
    }

    public async ValueTask<T[]> GetAllAsync<T>(string store) {
        var meta = Meta(store);
        await using var conn = await _source.OpenConnectionAsync().ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT * FROM {meta.Table} WHERE discord_id = @user;";
        cmd.Parameters.AddWithValue("user", await UserAsync());
        return await ReadAllAsync<T>(meta, cmd).ConfigureAwait(false);
    }

    public async ValueTask<T[]> GetAllByIndexAsync<T>(string store, string index, object value) {
        var meta = Meta(store);
        await using var conn = await _source.OpenConnectionAsync().ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT * FROM {meta.Table} WHERE discord_id = @user AND {Ident(index)} = @idx;";
        cmd.Parameters.AddWithValue("user", await UserAsync());
        cmd.Parameters.AddWithValue("idx", value ?? (object)DBNull.Value);
        return await ReadAllAsync<T>(meta, cmd).ConfigureAwait(false);
    }

    public async ValueTask DeleteAsync(string store, object key) {
        var meta = Meta(store);
        var (where, keyArgs) = KeyPredicate(meta, key);
        await using var conn = await _source.OpenConnectionAsync().ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM {meta.Table} WHERE discord_id = @user AND {where};";
        cmd.Parameters.AddWithValue("user", await UserAsync());
        BindKeyArgs(cmd, keyArgs);
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async ValueTask ClearAsync(string store) {
        var meta = Meta(store);
        await using var conn = await _source.OpenConnectionAsync().ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM {meta.Table} WHERE discord_id = @user;";
        cmd.Parameters.AddWithValue("user", await UserAsync());
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async ValueTask<int> CountAsync(string store) {
        var meta = Meta(store);
        await using var conn = await _source.OpenConnectionAsync().ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {meta.Table} WHERE discord_id = @user;";
        cmd.Parameters.AddWithValue("user", await UserAsync());
        var result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
        return result is null or DBNull ? 0 : Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private async Task UpsertAsync(StoreMeta meta, object value, NpgsqlConnection conn, NpgsqlTransaction? tx) {
        using var doc = JsonSerializer.SerializeToDocument(value, value.GetType(), JsonOpts);
        var user = await UserAsync().ConfigureAwait(false);

        // discord_id is always the first column; the row's own JSON columns follow.
        // The autoincrement column is skipped when null so Postgres assigns it.
        var cols = new List<string> { "discord_id" };
        var values = new List<(string Param, object Value)> { ("p_user", user) };
        int p = 0;
        foreach (var prop in doc.RootElement.EnumerateObject()) {
            if (meta.AutoIncrementColumn == prop.Name && prop.Value.ValueKind is JsonValueKind.Null) {
                continue;
            }
            var param = "p" + p.ToString(CultureInfo.InvariantCulture);
            cols.Add(prop.Name);
            values.Add((param, JsonToDbValue(prop.Value, meta.BlobColumns.Contains(prop.Name))));
            p++;
        }

        string conflict;
        if (meta.AutoIncrementColumn is not null) {
            conflict = "";
        } else {
            // Conflict target is (discord_id, <key columns>) since every table is
            // scoped by discord_id.
            var keyCols = new List<string> { "discord_id" };
            keyCols.AddRange(meta.KeyColumns);
            var updates = cols
                .Where(c => !keyCols.Contains(c, StringComparer.Ordinal))
                .Select(c => $"{Ident(c)} = excluded.{Ident(c)}");
            conflict = $" ON CONFLICT ({string.Join(", ", keyCols.Select(Ident))}) DO UPDATE SET {string.Join(", ", updates)}";
        }

        await using var cmd = conn.CreateCommand();
        if (tx is not null) {
            cmd.Transaction = tx;
        }
        var colSql = string.Join(", ", cols.Select(Ident));
        var valSql = string.Join(", ", values.Select(v => "@" + v.Param));
        cmd.CommandText = $"INSERT INTO {meta.Table} ({colSql}) VALUES ({valSql}){conflict};";
        foreach (var (param, val) in values) {
            if (val is byte[] bytes) {
                cmd.Parameters.Add(new NpgsqlParameter(param, NpgsqlDbType.Bytea) { Value = bytes });
            } else {
                cmd.Parameters.AddWithValue(param, val);
            }
        }
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task<T[]> ReadAllAsync<T>(StoreMeta meta, NpgsqlCommand cmd) {
        var result = new List<T>();
        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false)) {
            result.Add(Materialize<T>(meta, reader));
        }
        return [.. result];
    }

    // Rebuilds the row's JSON object from the SELECT columns (skipping discord_id, an
    // internal tenancy column not on the Row records), then deserializes to T.
    private static T Materialize<T>(StoreMeta meta, NpgsqlDataReader reader) {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer)) {
            writer.WriteStartObject();
            for (var i = 0; i < reader.FieldCount; i++) {
                var name = reader.GetName(i);
                if (name == "discord_id") {
                    continue;
                }
                writer.WritePropertyName(name);
                WriteColumn(writer, reader, i, meta.BlobColumns.Contains(name));
            }
            writer.WriteEndObject();
        }
        var json = buffer.ToArray();
        return JsonSerializer.Deserialize<T>(json, JsonOpts)
            ?? throw new InvalidOperationException($"failed to materialize row for {meta.Table}");
    }

    private static void WriteColumn(Utf8JsonWriter writer, NpgsqlDataReader reader, int i, bool isBlob) {
        if (reader.IsDBNull(i)) {
            writer.WriteNullValue();
            return;
        }
        if (isBlob) {
            writer.WriteBase64StringValue((byte[])reader.GetValue(i));
            return;
        }
        switch (reader.GetFieldType(i)) {
            case var t when t == typeof(bool):
                writer.WriteBooleanValue(reader.GetBoolean(i));
                break;
            case var t when t == typeof(long):
                writer.WriteNumberValue(reader.GetInt64(i));
                break;
            case var t when t == typeof(int):
                writer.WriteNumberValue(reader.GetInt32(i));
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
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
        JsonValueKind.String => DecodeString(el, isBlob),
        _ => el.GetRawText(),
    };

    // System.Text.Json serializes byte[] columns as base64 strings; BYTEA columns must
    // store real bytes. Plain text columns stay strings.
    private static object DecodeString(JsonElement el, bool isBlob) {
        var s = el.GetString() ?? "";
        return isBlob ? Convert.FromBase64String(s) : s;
    }

    private static void BindKeyArgs(NpgsqlCommand cmd, object[] args) {
        for (var i = 0; i < args.Length; i++) {
            cmd.Parameters.AddWithValue("k" + i.ToString(CultureInfo.InvariantCulture), args[i] ?? DBNull.Value);
        }
    }

    // KeyPredicate emits "col = @k0 AND col2 = @k1" (no discord_id; the caller prepends it).
    private static (string where, object[] args) KeyPredicate(StoreMeta meta, object key) {
        if (meta.KeyColumns.Length == 1) {
            return ($"{Ident(meta.KeyColumns[0])} = @k0", [key]);
        }
        if (key is object[] parts && parts.Length == meta.KeyColumns.Length) {
            var where = string.Join(" AND ",
                meta.KeyColumns.Select((c, i) => $"{Ident(c)} = @k{i.ToString(CultureInfo.InvariantCulture)}"));
            return (where, parts);
        }
        throw new ArgumentException($"composite key expected for store {meta.Table}", nameof(key));
    }

    // Quote identifiers defensively; all our names are already safe snake_case but the
    // index parameter to GetAllByIndexAsync is caller-supplied, so quote it.
    private static string Ident(string name) {
        if (name.Contains('"', StringComparison.Ordinal)) {
            throw new ArgumentException($"illegal identifier {name}", nameof(name));
        }
        return "\"" + name + "\"";
    }

    private StoreMeta Meta(string store) =>
        _stores.TryGetValue(store, out var meta)
            ? meta
            : throw new ArgumentException($"unknown store {store}", nameof(store));

    private static Dictionary<string, StoreMeta> BuildStoreMeta() => new(StringComparer.Ordinal) {
        ["mission"] = new StoreMeta("el_mission", ["player_id", "mission_id"], autoIncrementColumn: null,
            blobColumns: ["complete_payload"]),
        ["backup"] = new StoreMeta("el_backup", ["player_id"], autoIncrementColumn: null,
            blobColumns: ["payload"]),
        ["artifact_drops"] = new StoreMeta("el_artifact_drops", ["id"], autoIncrementColumn: "id",
            blobColumns: []),
        ["settings"] = new StoreMeta("el_settings", ["key"], autoIncrementColumn: null,
            blobColumns: []),
        ["reports"] = new StoreMeta("el_reports", ["id"], autoIncrementColumn: null,
            blobColumns: []),
        ["report_groups"] = new StoreMeta("el_report_groups", ["id"], autoIncrementColumn: null,
            blobColumns: []),
    };

    private sealed class StoreMeta {
        public StoreMeta(string table, string[] keyColumns, string? autoIncrementColumn, string[] blobColumns) {
            Table = table;
            KeyColumns = keyColumns;
            AutoIncrementColumn = autoIncrementColumn;
            BlobColumns = new HashSet<string>(blobColumns, StringComparer.Ordinal);
        }

        public string Table { get; }
        public string[] KeyColumns { get; }
        public string? AutoIncrementColumn { get; }
        public HashSet<string> BlobColumns { get; }
    }
}
