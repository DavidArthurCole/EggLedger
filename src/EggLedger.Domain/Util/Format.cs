using System.Globalization;

namespace EggLedger.Domain.Util;

/// <summary>
/// Number formatting helpers. C# Domain port of Go util/format.go. Pure.
/// </summary>
public static class Format
{
    private static readonly string[] Suffixes =
    {
        "", "K", "M", "B", "T", "q", "Q", "s", "S", "o", "N", "d", "U", "D", "Td", "qd", "Qd", "sd", "Sd", "od", "Nd",
    };

    /// <summary>
    /// Returns the order-of-magnitude suffix for the given exponent index.
    /// 0 -&gt; "", 1 -&gt; "K", 2 -&gt; "M", ... up to 20; anything above 20 -&gt; "!".
    /// </summary>
    public static string Addendum(int oom)
    {
        if (oom > 20)
        {
            return "!";
        }
        return Suffixes[oom];
    }

    /// <summary>
    /// Formats a large value as a short human-readable string,
    /// e.g. 1234567890 -&gt; "1.23B". Uses the same suffix table as Addendum.
    /// </summary>
    public static string AbbreviateFloat(double v)
    {
        var vCopy = v;
        var ooms = 0;
        while (vCopy >= 1e3 && ooms < 20)
        {
            vCopy /= 1e3;
            ooms++;
        }
        int precision;
        if (vCopy < 10.0)
        {
            precision = 2;
        }
        else if (vCopy < 100.0)
        {
            precision = 1;
        }
        else
        {
            precision = 0;
        }
        var formatted = vCopy.ToString("F" + precision.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        return formatted + Addendum(ooms);
    }
}
