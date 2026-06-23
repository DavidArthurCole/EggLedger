using System.Globalization;
using System.Text.RegularExpressions;

namespace EggLedger.Domain.Reports.Charts;

/// <summary>A plotted point with its source label and value.</summary>
public readonly record struct ChartPoint(double X, double Y, string Label, double Value);

/// <summary>A horizontal grid line tick: its y position and formatted label.</summary>
public readonly record struct YTick(double Y, string Label);

/// <summary>
/// Shared pure geometry for the line-family charts. Port of the must-be-correct
/// math in ReportLineChart.vue / ReportMultiLineChart.vue: value extraction,
/// point scaling, polyline / area paths, y-axis ticks, x-label thinning and
/// formatting, and the HSL series-color spread. SVG padding constants match the
/// Vue components exactly.
/// </summary>
public static partial class ChartGeometry
{
    public const double PadLeft = 36;
    public const double PadRight = 8;
    public const double PadTop = 10;
    public const double PadBottom = 60;
    public const int MaxLabels = 12;

    private static readonly string[] Months =
    {
        "Jan", "Feb", "Mar", "Apr", "May", "Jun",
        "Jul", "Aug", "Sep", "Oct", "Nov", "Dec",
    };

    /// <summary>
    /// Selects the value series for a result (float values when IsFloat, else the
    /// integer values widened to double). Port of the ReportLineChart values
    /// computed.
    /// </summary>
    public static List<double> Values(IReadOnlyList<long> values, IReadOnlyList<double> floatValues, bool isFloat) =>
        isFloat
            ? floatValues.ToList()
            : values.Select(v => (double)v).ToList();

    /// <summary>
    /// Scales values to plotted points across the chart width. Returns an empty
    /// list when there are fewer than 2 values (matching the Vue guard). Port of
    /// the ReportLineChart points computed.
    /// </summary>
    public static List<ChartPoint> Points(IReadOnlyList<double> values, IReadOnlyList<string> labels, double w, double h)
    {
        if (values.Count < 2)
        {
            return new List<ChartPoint>();
        }
        double max = Math.Max(values.Max(), 1);
        double xStep = (w - PadLeft - PadRight) / (values.Count - 1);
        double yRange = h - PadTop - PadBottom;
        var points = new List<ChartPoint>(values.Count);
        for (int i = 0; i < values.Count; i++)
        {
            double v = values[i];
            points.Add(new ChartPoint(
                PadLeft + i * xStep,
                PadTop + yRange * (1 - v / max),
                i < labels.Count ? labels[i] : "",
                v));
        }
        return points;
    }

    /// <summary>Space-separated "x,y" polyline points string.</summary>
    public static string PolylinePoints(IReadOnlyList<ChartPoint> points) =>
        string.Join(" ", points.Select(p => Fmt(p.X) + "," + Fmt(p.Y)));

    /// <summary>
    /// Closed area path from the polyline down to the baseline. Empty when fewer
    /// than 2 points. Port of the ReportLineChart areaPath computed.
    /// </summary>
    public static string AreaPath(IReadOnlyList<ChartPoint> points, double h)
    {
        if (points.Count < 2)
        {
            return "";
        }
        double baseline = PadTop + (h - PadTop - PadBottom);
        var line = string.Join(" ", points.Select((p, i) => (i == 0 ? "M" : "L") + Fmt(p.X) + "," + Fmt(p.Y)));
        var last = points[^1];
        var first = points[0];
        return $"{line} L{Fmt(last.X)},{Fmt(baseline)} L{Fmt(first.X)},{Fmt(baseline)} Z";
    }

    /// <summary>
    /// Thins point labels to at most <see cref="MaxLabels"/>, always keeping the
    /// last. Port of the ReportLineChart labeledPoints computed.
    /// </summary>
    public static List<ChartPoint> ThinLabels(IReadOnlyList<ChartPoint> points)
    {
        if (points.Count <= MaxLabels)
        {
            return points.ToList();
        }
        int step = (int)Math.Ceiling((double)points.Count / MaxLabels);
        var result = new List<ChartPoint>();
        for (int i = 0; i < points.Count; i++)
        {
            if (i % step == 0 || i == points.Count - 1)
            {
                result.Add(points[i]);
            }
        }
        return result;
    }

    /// <summary>
    /// Three y-axis ticks at 33%, 67%, 100% of the max. Empty when max is zero.
    /// Port of the ReportLineChart yTicks computed.
    /// </summary>
    public static List<YTick> YTicks(double max, double h, bool isFloat)
    {
        if (max == 0)
        {
            return new List<YTick>();
        }
        double yRange = h - PadTop - PadBottom;
        var fracs = new[] { 0.33, 0.67, 1.0 };
        var ticks = new List<YTick>(3);
        foreach (var frac in fracs)
        {
            double val = max * frac;
            double y = PadTop + yRange * (1 - frac);
            string label = isFloat
                ? val.ToString("0.0", CultureInfo.InvariantCulture)
                : Math.Round(val).ToString(CultureInfo.InvariantCulture);
            ticks.Add(new YTick(y, label));
        }
        return ticks;
    }

    /// <summary>
    /// Formats an x-axis label: "yyyy-MM" to "Mon 'yy" (or "Www 'yy" for week
    /// numbers > 12), "yyyy-MM-dd" to "d Mon", otherwise truncated. Port of the
    /// ReportLineChart formatXLabel.
    /// </summary>
    public static string FormatXLabel(string s)
    {
        var ym = YearMonthRegex().Match(s);
        if (ym.Success)
        {
            int n = int.Parse(ym.Groups[2].Value, CultureInfo.InvariantCulture);
            string yr = ym.Groups[1].Value.Substring(2);
            if (n is >= 1 and <= 12)
            {
                return $"{Months[n - 1]} '{yr}";
            }
            return $"W{ym.Groups[2].Value} '{yr}";
        }
        var ymd = YearMonthDayRegex().Match(s);
        if (ymd.Success)
        {
            int m = int.Parse(ymd.Groups[2].Value, CultureInfo.InvariantCulture);
            int d = int.Parse(ymd.Groups[3].Value, CultureInfo.InvariantCulture);
            return $"{d} {Months[m - 1]}";
        }
        return s.Length > 10 ? s.Substring(0, 9) + "..." : s;
    }

    /// <summary>
    /// HSL series colors spread evenly across the hue wheel from the base color,
    /// honoring per-label overrides. Returns CSS hsl() strings to match the Vue
    /// multi-line / grouped-bar series colors. Port of the ReportMultiLineChart
    /// seriesColors computed.
    /// </summary>
    public static List<string> SeriesColors(
        IReadOnlyList<string> colLabels,
        string baseColor,
        IReadOnlyDictionary<string, string> labelColors) =>
        SeriesColors(colLabels, baseColor, labelColors, colLabels.Count, null);

    /// <summary>
    /// Hue-spread series colors with an explicit hue denominator and optional
    /// "Other"-sentinel handling. The grouped-bar chart spreads display columns over
    /// the RAW column count (so kept columns keep the same hue when an "Other"
    /// rollup is present) and paints the "Other" column a fixed gray. The multi-line
    /// overload delegates here with the column count as the denominator and no
    /// sentinel. Per-label overrides still win for non-sentinel labels.
    /// </summary>
    public static List<string> SeriesColors(
        IReadOnlyList<string> colLabels,
        string baseColor,
        IReadOnlyDictionary<string, string> labelColors,
        int hueDenominator,
        (string Label, string Color)? otherSentinel)
    {
        int n = colLabels.Count;
        if (n == 0)
        {
            return new List<string>();
        }
        string baseHex = baseColor.StartsWith('#') && baseColor.Length == 7 ? baseColor : "#6366f1";
        var (h, s, _) = SliceColors.HexToHsl(baseHex);
        // The Vue multi-line uses a different hexToHsl returning h in degrees and
        // s in 0..100. SliceColors.HexToHsl returns s in 0..1, so scale here.
        double sPct = s * 100;
        double denom = hueDenominator <= 0 ? n : hueDenominator;
        var result = new List<string>(n);
        for (int i = 0; i < n; i++)
        {
            string label = colLabels[i];
            if (otherSentinel is { } sentinel && label == sentinel.Label)
            {
                result.Add(sentinel.Color);
                continue;
            }
            if (labelColors.TryGetValue(label, out var c) && !string.IsNullOrEmpty(c))
            {
                result.Add(c);
                continue;
            }
            double hue = (h + 360.0 / denom * i) % 360;
            result.Add(string.Format(
                CultureInfo.InvariantCulture,
                "hsl({0:0}, {1:0}%, 55%)",
                hue,
                sPct));
        }
        return result;
    }

    /// <summary>
    /// Extracts a single series' values from a row-major matrix (rows = buckets,
    /// cols = series). Port of the ReportMultiLineChart allSeriesData mapping.
    /// </summary>
    public static List<double> SeriesValues(IReadOnlyList<double> matrix, int rowCount, int colCount, int seriesIdx)
    {
        var result = new List<double>(rowCount);
        for (int r = 0; r < rowCount; r++)
        {
            int flat = r * colCount + seriesIdx;
            result.Add(flat < matrix.Count ? matrix[flat] : 0);
        }
        return result;
    }

    /// <summary>
    /// Escapes text for safe injection into SVG markup. Escapes the five XML
    /// special characters including quotes, so the result is safe in both element
    /// text and attribute contexts. Shared by the line and multi-line charts.
    /// </summary>
    public static string EscapeText(string s) =>
        s.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");

    private static string Fmt(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^(\d{4})-(\d{2})$")]
    private static partial Regex YearMonthRegex();

    [GeneratedRegex(@"^(\d{4})-(\d{2})-(\d{2})$")]
    private static partial Regex YearMonthDayRegex();
}
