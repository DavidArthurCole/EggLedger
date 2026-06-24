namespace EggLedger.Web.Data;

/// <summary>
/// IndexedDB-backed key-value settings store. C# port of the Go <c>db</c>
/// settings operations (<c>GetAllSettings</c>, <c>SetSetting</c>,
/// <c>SetSettings</c>) reading/writing the browser <c>settings</c> object store
/// (keyPath <c>key</c>) instead of the SQLite <c>settings</c> table. Writes are
/// upserts: IndexedDB <c>put</c> on a keyPath store replaces by key, matching
/// Go's INSERT ... ON CONFLICT(key) DO UPDATE.
/// </summary>
public sealed class IndexedDbSettings {
    private const string SettingsStore = "settings";

    private readonly IIndexedDb _db;

    public IndexedDbSettings(IIndexedDb db) {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <summary>
    /// All settings as a key-value map. Mirrors Go <c>GetAllSettings</c>: reads
    /// every row and folds it into a dictionary keyed by <c>key</c>.
    /// </summary>
    public async Task<Dictionary<string, string>> GetAllSettingsAsync() {
        var rows = await _db.GetAllAsync<SettingRow>(SettingsStore);
        var result = new Dictionary<string, string>(rows.Length);
        foreach (var row in rows) {
            result[row.Key] = row.Value;
        }
        return result;
    }

    /// <summary>
    /// Upserts a single setting. Mirrors Go <c>SetSetting</c>.
    /// </summary>
    public async Task SetSettingAsync(string key, string value) =>
        await _db.PutAsync(SettingsStore, new SettingRow { Key = key, Value = value });

    /// <summary>
    /// Upserts multiple settings in one batch. Mirrors Go <c>SetSettings</c>
    /// (single-transaction batch upsert).
    /// </summary>
    public async Task SetSettingsAsync(IReadOnlyDictionary<string, string> settings) {
        var rows = settings.Select(kv => (object)new SettingRow { Key = kv.Key, Value = kv.Value });
        await _db.PutManyAsync(SettingsStore, rows);
    }
}
