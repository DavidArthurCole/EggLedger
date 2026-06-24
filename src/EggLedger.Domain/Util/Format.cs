using System.Globalization;

namespace EggLedger.Domain.Util;

/// <summary>Number formatting helpers. Go port of util/format.go.</summary>
public static class Format {
    private static readonly string[] Suffixes =
    [
        "", "K", "M", "B", "T", "q", "Q", "s", "S", "o", "N", "d", "U", "D", "Td", "qd", "Qd", "sd", "Sd", "od", "Nd",
    ];

    /// <summary>Order-of-magnitude suffix for the exponent index (0..20); above 20 returns "!".</summary>
    public static string Addendum(int oom) {
        if (oom > 20) {
            return "!";
        }
        return Suffixes[oom];
    }

    /// <summary>Formats a large value as a short string, e.g. 1234567890 -&gt; "1.23B".</summary>
    public static string AbbreviateFloat(double v) {
        var vCopy = v;
        var ooms = 0;
        while (vCopy >= 1e3 && ooms < 20) {
            vCopy /= 1e3;
            ooms++;
        }
        int precision;
        if (vCopy < 10.0) {
            precision = 2;
        } else if (vCopy < 100.0) {
            precision = 1;
        } else {
            precision = 0;
        }
        var formatted = vCopy.ToString("F" + precision.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        return formatted + Addendum(ooms);
    }
}
