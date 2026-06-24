using System.Globalization;

namespace EggLedger.Domain.Reports;

/// <summary>
/// Time-bucket gap filling for sparse report series. Port of Go reports/timefill.go.
/// Week math reproduces SQLite strftime('%Y-%W') semantics exactly.
/// </summary>
public static class TimeFill {
    /// <summary>
    /// Inserts zero-value entries for missing time buckets between first and last.
    /// Inputs returned unchanged when the bucket type is unknown or labels unparseable.
    /// Port of Go fillTimeSeriesGaps.
    /// </summary>
    public static (List<string> labels, List<long> values) FillTimeSeriesGaps(
        string timeBucket, string customBucketUnit, List<string> rawLabels, List<long> values) {
        if (rawLabels.Count <= 1) {
            return (rawLabels, values);
        }
        var allBuckets = ExpandBucketRange(EffectiveBucketUnit(timeBucket, customBucketUnit), rawLabels[0], rawLabels[^1]);
        if (allBuckets == null || allBuckets.Count == rawLabels.Count) {
            return (rawLabels, values);
        }
        var lookup = new Dictionary<string, long>(rawLabels.Count);
        for (var i = 0; i < rawLabels.Count; i++) {
            lookup[rawLabels[i]] = values[i];
        }
        var outLabels = new List<string>(allBuckets.Count);
        var outV = new List<long>(allBuckets.Count);
        foreach (var b in allBuckets) {
            outLabels.Add(b);
            outV.Add(lookup.TryGetValue(b, out var v) ? v : 0);
        }
        return (outLabels, outV);
    }

    /// <summary>Float variant used by weighted time series. Port of Go fillTimeSeriesGapsFloat.</summary>
    public static (List<string> labels, List<double> values) FillTimeSeriesGapsFloat(
        string timeBucket, string customBucketUnit, List<string> rawLabels, List<double> values) {
        if (rawLabels.Count <= 1) {
            return (rawLabels, values);
        }
        var allBuckets = ExpandBucketRange(EffectiveBucketUnit(timeBucket, customBucketUnit), rawLabels[0], rawLabels[^1]);
        if (allBuckets == null || allBuckets.Count == rawLabels.Count) {
            return (rawLabels, values);
        }
        var lookup = new Dictionary<string, double>(rawLabels.Count);
        for (var i = 0; i < rawLabels.Count; i++) {
            lookup[rawLabels[i]] = values[i];
        }
        var outLabels = new List<string>(allBuckets.Count);
        var outV = new List<double>(allBuckets.Count);
        foreach (var b in allBuckets) {
            outLabels.Add(b);
            outV.Add(lookup.TryGetValue(b, out var v) ? v : 0);
        }
        return (outLabels, outV);
    }

    /// <summary>
    /// Inserts all-zero rows for missing time buckets in a (buckets x groups) matrix;
    /// nCols is the group-column count. Port of Go fillTimePivotGaps.
    /// </summary>
    public static (List<string> labels, double[] matrix) FillTimePivotGaps(
        string timeBucket, string customBucketUnit, List<string> bucketLabels, int nCols, double[] matrixValues) {
        if (bucketLabels.Count <= 1 || nCols == 0) {
            return (bucketLabels, matrixValues);
        }
        var allBuckets = ExpandBucketRange(EffectiveBucketUnit(timeBucket, customBucketUnit), bucketLabels[0], bucketLabels[^1]);
        if (allBuckets == null || allBuckets.Count == bucketLabels.Count) {
            return (bucketLabels, matrixValues);
        }
        var rowIndex = new Dictionary<string, int>(bucketLabels.Count);
        for (var i = 0; i < bucketLabels.Count; i++) {
            rowIndex[bucketLabels[i]] = i;
        }
        var newMatrix = new double[allBuckets.Count * nCols];
        for (var newR = 0; newR < allBuckets.Count; newR++) {
            if (rowIndex.TryGetValue(allBuckets[newR], out var oldR)) {
                Array.Copy(matrixValues, oldR * nCols, newMatrix, newR * nCols, nCols);
            }
        }
        return (allBuckets, newMatrix);
    }

    private static string EffectiveBucketUnit(string timeBucket, string customBucketUnit) =>
        timeBucket == "custom" ? customBucketUnit : timeBucket;

    /// <summary>
    /// Every time bucket label from first to last inclusive; null when the unit is
    /// unrecognised or labels unparseable. Port of Go expandBucketRange.
    /// </summary>
    private static List<string>? ExpandBucketRange(string unit, string first, string last) => unit switch {
        "day" => ExpandDayBuckets(first, last),
        "week" => ExpandWeekBuckets(first, last),
        "month" => ExpandMonthBuckets(first, last),
        "year" => ExpandYearBuckets(first, last),
        _ => null,
    };

    private static List<string>? ExpandDayBuckets(string first, string last) {
        if (!TryParseExact(first, "yyyy-MM-dd", out var t) || !TryParseExact(last, "yyyy-MM-dd", out var end)) {
            return null;
        }
        var outList = new List<string>();
        while (t <= end) {
            outList.Add(t.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            t = t.AddDays(1);
        }
        return outList;
    }

    private static List<string>? ExpandWeekBuckets(string first, string last) {
        if (!TryParseSQLiteWeekLabel(first, out var start) || !TryParseSQLiteWeekLabel(last, out var end)) {
            return null;
        }
        var outList = new List<string>();
        for (var t = start; t <= end; t = t.AddDays(7)) {
            outList.Add(FormatSQLiteWeekLabel(t));
        }
        return outList;
    }

    private static List<string>? ExpandMonthBuckets(string first, string last) {
        if (!TryParseExact(first, "yyyy-MM", out var t) || !TryParseExact(last, "yyyy-MM", out var end)) {
            return null;
        }
        var outList = new List<string>();
        while (t <= end) {
            outList.Add(t.ToString("yyyy-MM", CultureInfo.InvariantCulture));
            t = t.AddMonths(1);
        }
        return outList;
    }

    private static List<string>? ExpandYearBuckets(string first, string last) {
        if (!int.TryParse(first, NumberStyles.Integer, CultureInfo.InvariantCulture, out var y0)
            || !int.TryParse(last, NumberStyles.Integer, CultureInfo.InvariantCulture, out var y1)) {
            return null;
        }
        var outList = new List<string>();
        for (var y = y0; y <= y1; y++) {
            outList.Add(y.ToString("D4", CultureInfo.InvariantCulture));
        }
        return outList;
    }

    /// <summary>Parses "YYYY-WW" (SQLite %Y-%W) into the Monday that begins that week. Port of Go parseSQLiteWeekLabel.</summary>
    private static bool TryParseSQLiteWeekLabel(string label, out DateTime result) {
        result = default;
        var parts = label.Split('-', 2);
        if (parts.Length != 2) {
            return false;
        }
        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var year)
            || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var week)) {
            return false;
        }
        result = SQLiteWeekStart(year, week);
        return true;
    }

    /// <summary>
    /// First day of the given SQLite %W week: week 0 is days before the year's first
    /// Monday, week 1 starts on the first Monday. Port of Go sqliteWeekStart.
    /// </summary>
    private static DateTime SQLiteWeekStart(int year, int week) {
        var jan1 = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var jan1MondayOffset = ((int)jan1.DayOfWeek + 6) % 7; // Monday=0 ... Sunday=6
        var dayOfYear = (week * 7) - jan1MondayOffset + 1;
        if (dayOfYear < 1) {
            dayOfYear = 1;
        }
        return jan1.AddDays(dayOfYear - 1);
    }

    /// <summary>Formats t as "YYYY-WW" matching SQLite strftime('%Y-%W'). Port of Go formatSQLiteWeekLabel.</summary>
    private static string FormatSQLiteWeekLabel(DateTime t) {
        var jan1 = new DateTime(t.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var jan1MondayOffset = ((int)jan1.DayOfWeek + 6) % 7;
        var week = (t.DayOfYear - 1 + jan1MondayOffset) / 7;
        return string.Format(CultureInfo.InvariantCulture, "{0:D4}-{1:D2}", t.Year, week);
    }

    private static bool TryParseExact(string s, string fmt, out DateTime result) =>
        DateTime.TryParseExact(s, fmt, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result);
}
