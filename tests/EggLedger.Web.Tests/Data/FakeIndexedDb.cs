using EggLedger.Web.Data;

namespace EggLedger.Web.Tests.Data;

/// <summary>
/// In-memory <see cref="IIndexedDb"/> double. Holds one list of rows per store
/// and serves the index queries the store under test actually issues. Models the
/// <c>player_id</c> index on the <c>mission</c> store and the <c>account_id</c>
/// index on the <c>reports</c>/<c>report_groups</c> stores. Stores with a known
/// string keyPath (<c>reports</c>, <c>report_groups</c>) upsert on
/// <see cref="PutAsync"/> and support <see cref="GetAsync"/>/<see cref="DeleteAsync"/>.
/// </summary>
public sealed class FakeIndexedDb : IIndexedDb
{
    // The JS shim serializes every transaction on one browser thread; the lock
    // models that so concurrent fetch workers cannot corrupt a store list.
    private readonly Lock _gate = new();
    private readonly Dictionary<string, List<object>> _stores = new();

    /// <summary>Seeds one row into a store (always appends, no upsert).</summary>
    public void Seed(string store, object row)
    {
        lock (_gate)
        {
            if (!_stores.TryGetValue(store, out var list))
            {
                list = new List<object>();
                _stores[store] = list;
            }
            list.Add(row);
        }
    }

    /// <summary>The string key of a row for stores with a known keyPath, else null.</summary>
    private static string? KeyOf(object row) => row switch
    {
        ReportRow r => r.Id,
        ReportGroupRow g => g.Id,
        SettingRow s => s.Key,
        BackupRow b => b.PlayerId,
        _ => null,
    };

    public ValueTask<T[]> GetAllByIndexAsync<T>(string store, string index, object value)
    {
        lock (_gate)
        {
            if (!_stores.TryGetValue(store, out var list))
            {
                return new ValueTask<T[]>(Array.Empty<T>());
            }

            var matches = list.OfType<T>()
                .Where(row => IndexValue(row, index)?.Equals(value) == true)
                .ToArray();
            return new ValueTask<T[]>(matches);
        }
    }

    public ValueTask<T[]> GetAllAsync<T>(string store)
    {
        lock (_gate)
        {
            if (!_stores.TryGetValue(store, out var list))
            {
                return new ValueTask<T[]>(Array.Empty<T>());
            }
            return new ValueTask<T[]>(list.OfType<T>().ToArray());
        }
    }

    /// <summary>
    /// Resolves a row's index value. Supports the <c>player_id</c> index on
    /// <see cref="MissionRow"/>; throws for any other index so unmodelled queries
    /// fail loudly rather than silently returning nothing.
    /// </summary>
    private static object? IndexValue<T>(T row, string index) => (row, index) switch
    {
        (MissionRow m, "player_id") => m.PlayerId,
        (ReportRow r, "account_id") => r.AccountId,
        (ReportGroupRow g, "account_id") => g.AccountId,
        _ => throw new NotSupportedException($"FakeIndexedDb has no index '{index}' for {typeof(T).Name}"),
    };

    public ValueTask PutAsync(string store, object value)
    {
        lock (_gate)
        {
            PutLocked(store, value);
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask<int> PutManyAsync(string store, IEnumerable<object> values)
    {
        int n = 0;
        lock (_gate)
        {
            foreach (var v in values)
            {
                // Route through PutLocked so keyed stores upsert (matches the JS
                // shim's putMany, which is a per-row put on a keyPath store).
                PutLocked(store, v);
                n++;
            }
        }
        return new ValueTask<int>(n);
    }

    private void PutLocked(string store, object value)
    {
        if (!_stores.TryGetValue(store, out var list))
        {
            list = new List<object>();
            _stores[store] = list;
        }

        // Upsert by keyPath for stores with a known string key; append otherwise.
        var key = KeyOf(value);
        if (key is not null)
        {
            int i = list.FindIndex(r => KeyOf(r) == key);
            if (i >= 0)
            {
                list[i] = value;
                return;
            }
        }
        list.Add(value);
    }

    public ValueTask<T?> GetAsync<T>(string store, object key)
    {
        lock (_gate)
        {
            if (!_stores.TryGetValue(store, out var list))
            {
                return new ValueTask<T?>(default(T));
            }
            var match = list.OfType<T>().FirstOrDefault(r => KeyOf(r!)?.Equals(key) == true);
            return new ValueTask<T?>(match);
        }
    }

    public ValueTask DeleteAsync(string store, object key)
    {
        lock (_gate)
        {
            if (_stores.TryGetValue(store, out var list))
            {
                list.RemoveAll(r => KeyOf(r)?.Equals(key) == true);
            }
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask ClearAsync(string store)
    {
        lock (_gate)
        {
            _stores.Remove(store);
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask<int> CountAsync(string store)
    {
        lock (_gate)
        {
            return new(_stores.TryGetValue(store, out var list) ? list.Count : 0);
        }
    }
}
