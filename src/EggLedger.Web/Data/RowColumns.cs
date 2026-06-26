using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Serialization;

namespace EggLedger.Web.Data;

// Snake_case column names for a row record, read from [JsonPropertyName]. The SQL projected
// reads list these instead of SELECT * so a payload-free row type does not pull the blob.
public static class RowColumns {
    private static readonly ConcurrentDictionary<Type, string[]> _cache = new();

    public static IReadOnlyList<string> Of<T>() =>
        _cache.GetOrAdd(typeof(T), static t =>
            [.. t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
                .Where(n => n is not null)
                .Cast<string>()]);
}
