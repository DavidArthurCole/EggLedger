using System.Globalization;

namespace EggLedger.Domain.Reports.Charts;

/// <summary>A pie chart item after the top-N + "Other" rollup.</summary>
public readonly record struct PieItem(string Label, double Value);

/// <summary>A computed pie slice: its arc path, color, and percentage.</summary>
public readonly record struct PieSlice(string Label, double Value, double Pct, string Path, string Color);

/// <summary>
/// Pie chart geometry. Port of ReportPieChart.vue: top-N rollup, slice sweep angles,
/// and the SVG arc path. Label callout placement stays in the Razor component.
/// </summary>
public static class PieGeometry
{
    /// <summary>Maximum number of slices before the rest roll into "Other".</summary>
    public const int MaxSegments = 10;

    /// <summary>
    /// Pie items, rolling everything past <see cref="MaxSegments"/> - 1 into one "Other"
    /// slice (sorted by value desc before the cut); empty when total is zero. Port of ReportPieChart items.
    /// </summary>
    public static List<PieItem> BuildItems(IReadOnlyList<string> labels, IReadOnlyList<double> values)
    {
        double total = values.Sum();
        if (total == 0)
        {
            return [];
        }

        var items = new List<PieItem>(labels.Count);
        for (int i = 0; i < labels.Count; i++)
        {
            items.Add(new PieItem(labels[i], i < values.Count ? values[i] : 0));
        }

        if (items.Count > MaxSegments)
        {
            var sorted = items.OrderByDescending(x => x.Value).ToList();
            var kept = sorted.Take(MaxSegments - 1).ToList();
            double other = sorted.Skip(MaxSegments - 1).Sum(x => x.Value);
            kept.Add(new PieItem("Other", other));
            return kept;
        }
        return items;
    }

    /// <summary>SVG path for a pie slice from center, radius, and start/end angles (radians). Port of ReportPieChart slicePath.</summary>
    public static string SlicePath(double cx, double cy, double r, double startAngle, double endAngle)
    {
        double sx = cx + Math.Cos(startAngle) * r;
        double sy = cy + Math.Sin(startAngle) * r;
        double ex = cx + Math.Cos(endAngle) * r;
        double ey = cy + Math.Sin(endAngle) * r;
        int large = endAngle - startAngle > Math.PI ? 1 : 0;
        return string.Format(
            CultureInfo.InvariantCulture,
            "M {0} {1} L {2} {3} A {4} {5} 0 {6} 1 {7} {8} Z",
            cx, cy, sx, sy, r, r, large, ex, ey);
    }

    /// <summary>
    /// Colored slices for a result; colors from <see cref="SliceColors"/> with per-label
    /// overrides. Slices start at -90deg (12 o'clock) and sweep clockwise by value. Port of ReportPieChart segments.
    /// </summary>
    public static List<PieSlice> BuildSlices(
        IReadOnlyList<string> labels,
        IReadOnlyList<double> values,
        double cx,
        double cy,
        double r,
        string baseColor,
        IReadOnlyDictionary<string, string> labelColors)
    {
        var items = BuildItems(labels, values);
        var slices = new List<PieSlice>(items.Count);
        if (items.Count == 0)
        {
            return slices;
        }

        double total = items.Sum(i => i.Value);
        var autoColors = SliceColors.AutoSliceColors(baseColor, items.Count);
        double angleOffset = -Math.PI / 2;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            double sweep = total == 0 ? 0 : item.Value / total * 2 * Math.PI;
            double startAngle = angleOffset;
            double endAngle = angleOffset + sweep;
            angleOffset = endAngle;

            string color = labelColors.TryGetValue(item.Label, out var c) && !string.IsNullOrEmpty(c)
                ? c
                : autoColors[i];
            double pct = total == 0 ? 0 : item.Value / total * 100;
            slices.Add(new PieSlice(
                item.Label,
                item.Value,
                pct,
                SlicePath(cx, cy, r, startAngle, endAngle),
                color));
        }
        return slices;
    }
}
