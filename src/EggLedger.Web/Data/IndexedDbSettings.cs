namespace EggLedger.Web.Data;

public sealed class IndexedDbSettings {
    private readonly IIndexedDb _db;

    public IndexedDbSettings(IIndexedDb db) {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<Dictionary<string, string>> GetAllSettingsAsync() {
        var rows = await _db.GetAllAsync<SettingRow>(IndexedDbStores.Settings);
        var result = new Dictionary<string, string>(rows.Length);
        foreach (var row in rows) {
            result[row.Key] = row.Value;
        }
        return result;
    }

    public async Task SetSettingAsync(string key, string value) =>
        await _db.PutAsync(IndexedDbStores.Settings, new SettingRow { Key = key, Value = value });

    public async Task RemoveSettingAsync(string key) =>
        await _db.DeleteAsync(IndexedDbStores.Settings, key);

    public async Task SetSettingsAsync(IReadOnlyDictionary<string, string> settings) {
        var rows = settings.Select(kv => (object)new SettingRow { Key = kv.Key, Value = kv.Value });
        await _db.PutManyAsync(IndexedDbStores.Settings, rows);
    }
}
