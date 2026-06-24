using System.Globalization;
using System.Text.Json;

namespace EggLedger.Domain.Reports.Charts;

/// <summary>
/// Color math for pie and bar chart slices. Port of Vue useSliceColors.ts: hex/HSL
/// conversion and the auto hue-spread. Labels without an override fall back to the spread.
/// </summary>
public static class SliceColors {
    /// <summary>Converts #rrggbb to (hue[0..360), saturation[0..1], lightness[0..1]). Port of useSliceColors hexToHsl.</summary>
    public static (double H, double S, double L) HexToHsl(string hex) {
        double rv = ParseByte(hex, 1) / 255.0;
        double gv = ParseByte(hex, 3) / 255.0;
        double bv = ParseByte(hex, 5) / 255.0;
        double max = Math.Max(rv, Math.Max(gv, bv));
        double min = Math.Min(rv, Math.Min(gv, bv));
        double l = (max + min) / 2;
        double d = max - min;
        double s = d == 0 ? 0 : d / (1 - Math.Abs(2 * l - 1));
        double h = 0;
        if (d != 0) {
            if (max == rv) {
                h = Mod((gv - bv) / d + 6, 6);
            } else if (max == gv) {
                h = (bv - rv) / d + 2;
            } else {
                h = (rv - gv) / d + 4;
            }
            h *= 60;
        }
        return (h, s, l);
    }

    /// <summary>Converts HSL (hue degrees, s[0..1], l[0..1]) to #rrggbb. Port of useSliceColors hslToHex.</summary>
    public static string HslToHex(double h, double s, double l) {
        double a = s * Math.Min(l, 1 - l);
        string F(int n) {
            double k = Mod(n + h / 30, 12);
            double color = l - a * Math.Max(Math.Min(Math.Min(k - 3, 9 - k), 1), -1);
            int v = (int)Math.Round(255 * color);
            return v.ToString("x2", CultureInfo.InvariantCulture);
        }
        return $"#{F(0)}{F(8)}{F(4)}";
    }

    /// <summary><paramref name="count"/> colors spread across the hue wheel from the base color's hue. Port of useSliceColors autoSliceColors.</summary>
    public static IReadOnlyList<string> AutoSliceColors(string baseColor, int count) {
        var (h, s, l) = HexToHsl(baseColor);
        var result = new List<string>(Math.Max(count, 0));
        for (int i = 0; i < count; i++) {
            result.Add(HslToHex(Mod(h + (double)i * 360 / count, 360), s, l));
        }
        return result;
    }

    /// <summary>Parses a JSON label-to-hex map; empty map on blank or invalid input. Port of useSliceColors parseLabelColors.</summary>
    public static Dictionary<string, string> ParseLabelColors(string? raw) {
        if (string.IsNullOrEmpty(raw)) {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
        try {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(raw);
            return parsed ?? new Dictionary<string, string>(StringComparer.Ordinal);
        } catch (JsonException) {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Color for a label: explicit override, else the hue-spread slot for the label's
    /// index, else the base color. Port of useSliceColors getLabelColor.
    /// </summary>
    public static string GetLabelColor(
        string label,
        string baseColor,
        IReadOnlyList<string> chartLabels,
        IReadOnlyDictionary<string, string> labelColorsMap) {
        if (labelColorsMap.TryGetValue(label, out var overridden) && !string.IsNullOrEmpty(overridden)) {
            return overridden;
        }
        int idx = IndexOf(chartLabels, label);
        var colors = AutoSliceColors(baseColor, chartLabels.Count);
        if (idx >= 0 && idx < colors.Count) {
            return colors[idx];
        }
        return baseColor;
    }

    private static int IndexOf(IReadOnlyList<string> list, string value) {
        for (int i = 0; i < list.Count; i++) {
            if (list[i] == value) {
                return i;
            }
        }
        return -1;
    }

    private static int ParseByte(string hex, int start) =>
        int.Parse(hex.AsSpan(start, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    // Non-negative modulo, matching the Vue hue-math contract for any input.
    private static double Mod(double a, double n) {
        double r = a % n;
        return r < 0 ? r + n : r;
    }
}
