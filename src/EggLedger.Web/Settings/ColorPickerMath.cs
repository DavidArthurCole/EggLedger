using System.Globalization;
using System.Text.RegularExpressions;
using EggLedger.Domain.Reports.Charts;

namespace EggLedger.Web.Settings;

/// <summary>
/// Pure color math for the Settings color picker. C# port of the pure parts of
/// www/src/components/ColorPicker.vue: hex validation, normalize-any-to-hex, the
/// preset palette, and the wheel/dot coordinate math. The hex/HSL conversions
/// reuse <see cref="SliceColors"/> (already a port of the same JS formulas) rather
/// than duplicating them; the picker works in integer HSL percentages, so this
/// class adapts SliceColors' [0..1] saturation/lightness at the boundary.
/// </summary>
public static partial class ColorPickerMath
{
    /// <summary>The 24 preset swatches, in the Vue ColorPicker grid order.</summary>
    public static readonly IReadOnlyList<string> PresetColors =
    [
        "#f43f5e", "#ef4444", "#f97316", "#f59e0b",
        "#22c55e", "#10b981", "#14b8a6", "#06b6d4",
        "#3b82f6", "#6366f1", "#8b5cf6", "#a855f7",
        "#d946ef", "#ec4899", "#64748b", "#94a3b8",
        "#e2e8f0", "#fbbf24", "#fb923c", "#c084fc",
        "#60a5fa", "#34d399", "#f9a8d4", "#ffffff",
    ];

    /// <summary>The Vue fallback color used when input is unparseable.</summary>
    public const string Fallback = "#6366f1";

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexRegexGen();

    [GeneratedRegex(@"hsl\(\s*([\d.]+)\s*,\s*([\d.]+)%\s*,\s*([\d.]+)%\s*\)")]
    private static partial Regex HslRegexGen();

    /// <summary>True for a valid <c>#rrggbb</c> string. Port of the Vue hexRegex test.</summary>
    public static bool IsValidHex(string? value) =>
        value is not null && HexRegexGen().IsMatch(value);

    /// <summary>
    /// Integer-percentage HSL, the picker's source of truth for the wheel + sliders.
    /// Hue in [0, 360], saturation/lightness in [0, 100].
    /// </summary>
    public readonly record struct HslInt(int H, int S, int L);

    /// <summary>
    /// Parses any supported color string to <c>#rrggbb</c>: a hex literal (lowered),
    /// an <c>hsl(h, s%, l%)</c> string, else the fallback. Port of the Vue
    /// normalizeToHex.
    /// </summary>
    public static string NormalizeToHex(string? value)
    {
        if (value is null)
        {
            return Fallback;
        }
        if (HexRegexGen().IsMatch(value))
        {
            return value.ToLowerInvariant();
        }
        var m = HslRegexGen().Match(value);
        if (m.Success)
        {
            double h = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            double s = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            double l = double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            return HslToHex(h, s, l);
        }
        return Fallback;
    }

    /// <summary>
    /// Converts integer-percentage HSL to <c>#rrggbb</c>. Delegates to
    /// <see cref="SliceColors.HslToHex"/> (S/L scaled to [0..1]). Port of the Vue
    /// hslToHex.
    /// </summary>
    public static string HslToHex(double h, double s, double l) =>
        SliceColors.HslToHex(h, s / 100.0, l / 100.0);

    /// <summary>
    /// Parses <c>#rrggbb</c> to integer-percentage HSL. Wraps
    /// <see cref="SliceColors.HexToHsl"/> and rounds to integer percentages, matching
    /// the Vue hexToHsl (which rounds h/s/l to integers).
    /// </summary>
    public static HslInt HexToHslInt(string hex)
    {
        var (h, s, l) = SliceColors.HexToHsl(hex);
        return new HslInt(
            (int)Math.Round(h),
            (int)Math.Round(s * 100),
            (int)Math.Round(l * 100));
    }

    /// <summary>
    /// Maps a wheel hit (cursor delta from centre, plus wheel radius) to the
    /// resulting hue/saturation. Port of the Vue pickWheelColor math: angle is
    /// atan2 + 90 deg wrapped to [0, 360), saturation is the clamped radial
    /// distance as a percent. Lightness is unchanged (caller keeps it).
    /// </summary>
    public static (int H, int S) WheelHueSaturation(double dx, double dy, double radius)
    {
        double rawAngle = Math.Atan2(dy, dx) * (180 / Math.PI) + 90;
        double hue = Mod(rawAngle, 360);
        double dist = Math.Sqrt(dx * dx + dy * dy);
        double ratio = radius <= 0 ? 0 : Math.Min(dist / radius, 1);
        int sat = (int)Math.Round(ratio * 100);
        return ((int)Math.Round(hue), sat);
    }

    /// <summary>
    /// Position (percent of the wheel box) of the selector dot for a hue/saturation.
    /// Port of the Vue dotStyle computed: angle is (h - 90) deg, radius is
    /// saturation scaled to ~45% of the box, centred at 50%/50%.
    /// </summary>
    public static (double LeftPct, double TopPct) DotPosition(int hue, int saturation)
    {
        double rad = (hue - 90) * Math.PI / 180;
        double r = saturation / 100.0 * 45;
        return (50 + r * Math.Cos(rad), 50 + r * Math.Sin(rad));
    }

    private static double Mod(double a, double n)
    {
        double r = a % n;
        return r < 0 ? r + n : r;
    }
}
