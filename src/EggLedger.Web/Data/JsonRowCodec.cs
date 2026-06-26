using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EggLedger.Web.Data;

/// <summary>
/// JSON&lt;-&gt;DB row marshaling shared by the SQL <see cref="IIndexedDb"/> impls (Postgres,
/// SQLite). Rows round-trip through their snake_case JSON property names; each impl keeps
/// only its provider-specific SQL execution and supplies the two differences via a
/// <see cref="Provider"/>: how bools encode (native vs 1/0) and the key-predicate placeholder
/// syntax (<c>@k0</c> vs <c>?</c>). Wire format (base64 blobs, column names) must not change.
/// </summary>
public static class JsonRowCodec {
    /// <param name="BoolAsInteger">SQLite stores bools as 1/0 INTEGER; Postgres uses native bool.</param>
    /// <param name="KeyPlaceholder">Placeholder for the i-th key arg: SQLite <c>?</c>, Postgres <c>@k{i}</c>.</param>
    public readonly record struct Provider(bool BoolAsInteger, Func<int, string> KeyPlaceholder);
    public static readonly Provider Sqlite = new(BoolAsInteger: true, _ => "?");
    public static readonly Provider Postgres = new(BoolAsInteger: false, i => "@k" + i.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Rebuilds a row's JSON object from the SELECT columns, then deserializes to T. The
    /// <paramref name="skipColumn"/> predicate drops internal columns not on the Row record
    /// (Postgres tenancy <c>discord_id</c>); <paramref name="isBool"/>/<paramref name="isBlob"/>
    /// classify columns whose reader type is ambiguous (SQLite bool-as-int, blob-as-base64).
    /// </summary>
    public static T Materialize<T>(
        DbDataReader reader,
        string table,
        Func<string, bool> isBool,
        Func<string, bool> isBlob,
        JsonSerializerOptions jsonOpts,
        Func<string, bool>? skipColumn = null) {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer)) {
            writer.WriteStartObject();
            for (var i = 0; i < reader.FieldCount; i++) {
                var name = reader.GetName(i);
                if (skipColumn is not null && skipColumn(name)) {
                    continue;
                }
                writer.WritePropertyName(name);
                WriteColumn(writer, reader, i, isBool(name), isBlob(name));
            }
            writer.WriteEndObject();
        }
        var json = buffer.ToArray();
        return JsonSerializer.Deserialize<T>(json, jsonOpts)
            ?? throw new InvalidOperationException($"failed to materialize row for {table}");
    }

    public static void WriteColumn(Utf8JsonWriter writer, DbDataReader reader, int i, bool isBool, bool isBlob) {
        if (reader.IsDBNull(i)) {
            writer.WriteNullValue();
            return;
        }
        if (isBlob) {
            writer.WriteBase64StringValue((byte[])reader.GetValue(i));
            return;
        }
        if (isBool) {
            writer.WriteBooleanValue(reader.GetInt64(i) != 0);
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

    public static object JsonToDbValue(JsonElement el, bool isBlob, Provider provider) => el.ValueKind switch {
        JsonValueKind.Null => DBNull.Value,
        JsonValueKind.True => provider.BoolAsInteger ? 1L : true,
        JsonValueKind.False => provider.BoolAsInteger ? 0L : false,
        JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
        JsonValueKind.String => DecodeString(el, isBlob),
        _ => el.GetRawText(),
    };

    // System.Text.Json serializes byte[] columns as base64 strings; blob columns must
    // store real bytes. Plain text columns stay strings.
    public static object DecodeString(JsonElement el, bool isBlob) {
        var s = el.GetString() ?? "";
        return isBlob ? Convert.FromBase64String(s) : s;
    }

    /// <summary>
    /// Single-key predicate emits "col = ?0"; composite expects an object[] of matching
    /// length. No tenancy scoping (the Postgres caller prepends discord_id itself).
    /// </summary>
    public static (string where, object[] args) KeyPredicate(
        string table, string[] keyColumns, object key, Func<string, string> ident, Provider provider) {
        if (keyColumns.Length == 1) {
            return ($"{ident(keyColumns[0])} = {provider.KeyPlaceholder(0)}", [key]);
        }
        if (key is object[] parts && parts.Length == keyColumns.Length) {
            var where = string.Join(" AND ",
                keyColumns.Select((c, i) => $"{ident(c)} = {provider.KeyPlaceholder(i)}"));
            return (where, parts);
        }
        throw new ArgumentException($"composite key expected for store {table}", nameof(key));
    }
}
