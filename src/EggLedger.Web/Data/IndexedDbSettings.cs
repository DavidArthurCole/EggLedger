namespace EggLedger.Web.Data;

/// <summary>
/// IndexedDB-backed key-value settings store (Go <c>db</c> settings ops). Writes are
/// upserts: <c>put</c> on the keyPath store replaces by key, matching Go's INSERT ... ON CONFLICT.
/// </summary>
public sealed class IndexedDbSettings {
    private const string SettingsStore = "settings";

    private readonly IIndexedDb _db;

    public IndexedDbSettings(IIndexedDb db) {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<Dictionary<string, string>> GetAllSettingsAsync() {
        var rows = await _db.GetAllAsync<SettingRow>(SettingsStore);
        var result = new Dictionary<string, string>(rows.Length);
        foreach (var row in rows) {
            result[row.Key] = row.Value;
        }
        return result;
    }

    public async Task SetSettingAsync(string key, string value) =>
        await _db.PutAsync(SettingsStore, new SettingRow { Key = key, Value = value });

    /// <summary>Single-transaction batch upsert. Mirrors Go <c>SetSettings</c>.</summary>
    public async Task SetSettingsAsync(IReadOnlyDictionary<string, string> settings) {
        var rows = settings.Select(kv => (object)new SettingRow { Key = kv.Key, Value = kv.Value });
        await _db.PutManyAsync(SettingsStore, rows);
    }
}
