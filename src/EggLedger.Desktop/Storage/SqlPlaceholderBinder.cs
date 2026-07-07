using System.Globalization;
using Microsoft.Data.Sqlite;

namespace EggLedger.Desktop.Storage;

/// <summary>Rewrites positional "?" SQL placeholders (the shape IIndexedDb/IMissionDb callers pass) into the named parameters Microsoft.Data.Sqlite binds, in one pass over the command text.</summary>
public static class SqlPlaceholderBinder {
    /// <summary>
    /// Replaces each "?" in <paramref name="sql"/> with "@{paramPrefix}N" bound to
    /// <c>args[N]</c> (DBNull for a missing arg), and adds the parameter to
    /// <paramref name="cmd"/>. Throws if the placeholder count does not match
    /// <paramref name="args"/>.Count.
    /// </summary>
    public static string Rewrite(string sql, IReadOnlyList<object?> args, SqliteCommand cmd, string paramPrefix) {
        var result = sql;
        var idx = 0;
        while (true) {
            var pos = result.IndexOf('?', StringComparison.Ordinal);
            if (pos < 0) {
                break;
            }
            var name = "@" + paramPrefix + idx.ToString(CultureInfo.InvariantCulture);
            result = result.Remove(pos, 1).Insert(pos, name);
            cmd.Parameters.AddWithValue(name, idx < args.Count ? args[idx] ?? DBNull.Value : DBNull.Value);
            idx++;
        }
        if (idx != args.Count) {
            throw new InvalidOperationException(
                $"placeholder/arg mismatch: {idx} '?' placeholders but {args.Count} args");
        }
        return result;
    }
}
