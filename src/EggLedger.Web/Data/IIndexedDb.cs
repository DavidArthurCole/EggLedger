namespace EggLedger.Web.Data;

// Key-value object-store seam shared by all three persistence backends (browser IndexedDB,
// Postgres, SQLite). store/index names come from IndexedDbStores; values + keys are the typed
// Row records the stores own. The JSON<->column round-trip is fixed by JsonRowCodec.
public interface IIndexedDb {
    ValueTask PutAsync(string store, object value);
    ValueTask<int> PutManyAsync(string store, IEnumerable<object> values);
    ValueTask<T?> GetAsync<T>(string store, object key);
    ValueTask<T[]> GetAllAsync<T>(string store);
    ValueTask<T[]> GetAllByIndexAsync<T>(string store, string index, object value);
    ValueTask DeleteAsync(string store, object key);
    ValueTask ClearAsync(string store);
    ValueTask<int> CountAsync(string store);
}
