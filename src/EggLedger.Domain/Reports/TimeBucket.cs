using System.Globalization;

namespace EggLedger.Domain.Reports;

/// <summary>
/// In-memory reproduction of the SQLite time expressions the report queries use,
/// all UTC, so the SQL-free path produces identical bucket strings and date comparisons.
/// </summary>
internal static class TimeBucket {
    private static readonly DateTime Epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Bucket label for a unix timestamp, matching strftime(TimeBucketFormat(...), datetime(ts, 'unixepoch')).</summary>
    public static string Format(string timeBucket, string customUnit, long unixSeconds) {
        var fmt = QueryBuilder.TimeBucketFormat(timeBucket, customUnit);
        var t = Epoch.AddSeconds(unixSeconds);
        return fmt switch {
            "%Y-%m-%d" => t.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "%Y-%m" => t.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            "%Y" => t.ToString("yyyy", CultureInfo.InvariantCulture),
            "%Y-%W" => WeekLabel(t),
            _ => t.ToString("yyyy-MM", CultureInfo.InvariantCulture),
        };
    }

    /// <summary>
    /// SQLite strftime('%Y-%W') label: week 0 is days before the year's first Monday,
    /// week 1 starts on the first Monday. Mirrors TimeFill.FormatSQLiteWeekLabel.
    /// </summary>
    private static string WeekLabel(DateTime t) {
        var jan1 = new DateTime(t.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var jan1MondayOffset = ((int)jan1.DayOfWeek + 6) % 7;
        var week = (t.DayOfYear - 1 + jan1MondayOffset) / 7;
        return string.Format(CultureInfo.InvariantCulture, "{0:D4}-{1:D2}", t.Year, week);
    }

    /// <summary>
    /// Parses a date-filter value to a unix timestamp (strftime('%s', value)). Accepts
    /// "YYYY-MM-DD" (midnight UTC) and "YYYY-MM-DD HH:MM:SS"; false on an unrecognized date.
    /// </summary>
    public static bool TryParseDateToUnix(string value, out long unixSeconds) {
        unixSeconds = 0;
        string[] formats =
        [
            "yyyy-MM-dd",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss",
        ];
        if (!DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t)) {
            return false;
        }
        unixSeconds = (long)(t - Epoch).TotalSeconds;
        return true;
    }

    /// <summary>
    /// Reproduces strftime('%s', 'now', modifier) for the custom-window cutoff using
    /// UTC now; modifier is "-N days" or "-N months" from CustomWindowCondition.
    /// </summary>
    public static long NowMinus(string modifier) {
        var now = DateTime.UtcNow;
        var parts = modifier.Split(' ');
        if (parts.Length == 2 && int.TryParse(parts[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n)) {
            now = parts[1] switch {
                "days" => now.AddDays(n),
                "months" => now.AddMonths(n),
                _ => now,
            };
        }
        return (long)(now - Epoch).TotalSeconds;
    }
}
