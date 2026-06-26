using EggLedger.Web.Data;

namespace EggLedger.Web.Tests.Data;

/// <summary>
/// In-memory <see cref="IIndexedDb"/> double: one list of rows per store, serving the
/// index queries the stores under test issue (player_id on mission, account_id on reports/report_groups).
/// Stores with a known string keyPath upsert on Put and support Get/Delete.
/// </summary>
public sealed class FakeIndexedDb : IIndexedDb {
    // Models the JS shim serializing every transaction on one browser thread, so concurrent
    // fetch workers cannot corrupt a store list.
    private readonly Lock _gate = new();
    private readonly Dictionary<string, List<object>> _stores = [];

    /// <summary>Seeds one row into a store (always appends, no upsert).</summary>
    public void Seed(string store, object row) {
        lock (_gate) {
            if (!_stores.TryGetValue(store, out var list)) {
                list = [];
                _stores[store] = list;
            }
            list.Add(row);
        }
    }

    /// <summary>The string key of a row for stores with a known keyPath, else null.</summary>
    private static string? KeyOf(object row) => row switch {
        ReportRow r => r.Id,
        ReportGroupRow g => g.Id,
        SettingRow s => s.Key,
        BackupRow b => b.PlayerId,
        _ => null,
    };

    public ValueTask<T[]> GetAllByIndexAsync<T>(string store, string index, object value) {
        lock (_gate) {
            if (!_stores.TryGetValue(store, out var list)) {
                return new ValueTask<T[]>([]);
            }

            var matches = list.OfType<T>()
                .Where(row => IndexValue(row, index)?.Equals(value) == true)
                .ToArray();
            return new ValueTask<T[]>(matches);
        }
    }

    public ValueTask<T[]> GetAllAsync<T>(string store) {
        lock (_gate) {
            if (!_stores.TryGetValue(store, out var list)) {
                return new ValueTask<T[]>([]);
            }
            return new ValueTask<T[]>([.. list.OfType<T>()]);
        }
    }

    /// <summary>
    /// Resolves a row's index value. Throws for any unmodelled index so those queries fail loudly
    /// rather than silently returning nothing.
    /// </summary>
    private static object? IndexValue<T>(T row, string index) => (row, index) switch {
        (MissionRow m, "player_id") => m.PlayerId,
        (ReportRow r, "account_id") => r.AccountId,
        (ReportGroupRow g, "account_id") => g.AccountId,
        _ => throw new NotSupportedException($"FakeIndexedDb has no index '{index}' for {typeof(T).Name}"),
    };

    public ValueTask PutAsync(string store, object value) {
        lock (_gate) {
            PutLocked(store, value);
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask<int> PutManyAsync(string store, IEnumerable<object> values) {
        int n = 0;
        lock (_gate) {
            foreach (var v in values) {
                // PutLocked so keyed stores upsert, matching the JS shim's per-row putMany.
                PutLocked(store, v);
                n++;
            }
        }
        return new ValueTask<int>(n);
    }

    private void PutLocked(string store, object value) {
        if (!_stores.TryGetValue(store, out var list)) {
            list = [];
            _stores[store] = list;
        }

        // Upsert by keyPath for stores with a known string key; append otherwise.
        var key = KeyOf(value);
        if (key is not null) {
            int i = list.FindIndex(r => KeyOf(r) == key);
            if (i >= 0) {
                list[i] = value;
                return;
            }
        }
        list.Add(value);
    }

    public ValueTask<T?> GetAsync<T>(string store, object key) {
        lock (_gate) {
            if (!_stores.TryGetValue(store, out var list)) {
                return new ValueTask<T?>(default(T));
            }
            var match = list.OfType<T>().FirstOrDefault(r => KeyOf(r!)?.Equals(key) == true);
            return new ValueTask<T?>(match);
        }
    }

    public ValueTask DeleteAsync(string store, object key) {
        lock (_gate) {
            if (_stores.TryGetValue(store, out var list)) {
                list.RemoveAll(r => KeyOf(r)?.Equals(key) == true);
            }
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask ClearAsync(string store) {
        lock (_gate) {
            _stores.Remove(store);
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask<int> CountAsync(string store) {
        lock (_gate) {
            return new(_stores.TryGetValue(store, out var list) ? list.Count : 0);
        }
    }
}
