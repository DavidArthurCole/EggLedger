using System.Globalization;

namespace EggLedger.Domain.Reports.Charts;

/// <summary>A heatmap cell's computed background and text color.</summary>
public readonly record struct HeatmapCellStyle(string BackgroundColor, string Color);

/// <summary>
/// Pure heatmap cell math. Port of the must-be-correct parts of
/// ReportHeatmap.vue: intensity, base/unfilled color blending, relative
/// luminance text-color choice, and the below-sample-threshold check. The drag
/// reorder and DOM wiring stay in the Razor component.
/// </summary>
public static class HeatmapGeometry
{
    /// <summary>Whether a normalizeBy mode renders cells as percentages.</summary>
    public static bool IsPct(string? normalizeBy) =>
        normalizeBy is "row_pct" or "col_pct" or "global_pct";

    /// <summary>Validates a #rrggbb color, falling back to a default. Port of the safeColor computed.</summary>
    public static string SafeColor(string? color, string fallback) =>
        color is not null && color.StartsWith('#') && color.Length == 7 ? color : fallback;

    /// <summary>Parses a #rrggbb hex into (r, g, b) byte components. Port of hexToRgb.</summary>
    public static (int R, int G, int B) HexToRgb(string hex) => (
        int.Parse(hex.Substring(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
        int.Parse(hex.Substring(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
        int.Parse(hex.Substring(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));

    /// <summary>WCAG relative luminance of an sRGB color. Port of relativeLuminance.</summary>
    public static double RelativeLuminance(int r, int g, int b)
    {
        static double ToLinear(int c)
        {
            double s = c / 255.0;
            return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * ToLinear(r) + 0.7152 * ToLinear(g) + 0.0722 * ToLinear(b);
    }

    /// <summary>
    /// Cell fill intensity in [0.12, 1]. Color-values override raw value; pct and
    /// ratio modes have their own scales; default is value / globalMax. Port of
    /// cellIntensity.
    /// </summary>
    public static double CellIntensity(
        double value,
        double globalMax,
        string? normalizeBy,
        double? colorValue)
    {
        if (colorValue is double cv)
        {
            return Math.Max(0.12, cv > 0 ? Math.Min(cv / 2, 1) : 0);
        }
        if (IsPct(normalizeBy))
        {
            return Math.Max(0.12, value / 100);
        }
        if (normalizeBy == "ratio")
        {
            return Math.Max(0.12, Math.Min(value / 2, 1));
        }
        return Math.Max(0.12, globalMax > 0 ? value / globalMax : 0);
    }

    /// <summary>Whether a cell is below the minimum sample size. Port of isBelowThreshold.</summary>
    public static bool IsBelowThreshold(int? missionCount, int minSampleSize)
    {
        if (minSampleSize <= 0 || missionCount is null)
        {
            return false;
        }
        return missionCount.Value < minSampleSize;
    }

    /// <summary>
    /// Computes a cell's background and text color, blending base over unfilled by
    /// intensity and choosing dark or light text by luminance. Empty / below-
    /// threshold cells use the unfilled color. Port of the cellStyle computed
    /// (without the hover brightness filter, which is applied in CSS).
    /// </summary>
    public static HeatmapCellStyle CellStyle(
        double value,
        double globalMax,
        string baseColor,
        string unfilledColor,
        string? normalizeBy,
        double? colorValue,
        bool belowThreshold)
    {
        string safeBase = SafeColor(baseColor, "#6366f1");
        string safeUnfilled = SafeColor(unfilledColor, "#1f2937");

        if (belowThreshold || value == 0)
        {
            var (ur, ug, ub) = HexToRgb(safeUnfilled);
            return new HeatmapCellStyle(
                safeUnfilled,
                RelativeLuminance(ur, ug, ub) > 0.179 ? "#1f2937" : "#6b7280");
        }

        double intensity = CellIntensity(value, globalMax, normalizeBy, colorValue);
        var (fr, fg, fb) = HexToRgb(safeBase);
        var (br, bg, bb) = HexToRgb(safeUnfilled);
        int r = (int)Math.Round(br + (fr - br) * intensity);
        int g = (int)Math.Round(bg + (fg - bg) * intensity);
        int b = (int)Math.Round(bb + (fb - bb) * intensity);
        return new HeatmapCellStyle(
            $"rgb({r}, {g}, {b})",
            RelativeLuminance(r, g, b) > 0.179 ? "#1f2937" : "#f3f4f6");
    }

    /// <summary>
    /// Display string for a cell value honoring ratio / pct / integer / float
    /// formatting. Below-threshold returns "-". Port of displayValue.
    /// </summary>
    public static string DisplayValue(double value, string? normalizeBy, bool belowThreshold)
    {
        if (belowThreshold)
        {
            return "-";
        }
        if (normalizeBy == "ratio")
        {
            if (value == 0)
            {
                return "-";
            }
            return "x" + value.ToString("0.00", CultureInfo.InvariantCulture);
        }
        if (value == 0)
        {
            return "0";
        }
        if (IsPct(normalizeBy))
        {
            return value.ToString("0.0", CultureInfo.InvariantCulture) + "%";
        }
        return value % 1 == 0
            ? ((long)value).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Display string for the optional secondary sub-line (Menno dual_value mode).
    /// Below-threshold returns empty (the sub-line is hidden); otherwise it formats
    /// the value as 0 / pct / integer / float, matching the primary value's number
    /// formatting minus the ratio case. Port of secondaryDisplayValue.
    /// </summary>
    public static string SecondaryDisplayValue(double value, string? normalizeBy, bool belowThreshold)
    {
        if (belowThreshold)
        {
            return "";
        }
        if (value == 0)
        {
            return "0";
        }
        if (IsPct(normalizeBy))
        {
            return value.ToString("0.0", CultureInfo.InvariantCulture) + "%";
        }
        return value % 1 == 0
            ? ((long)value).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
