using System.Globalization;

namespace EggLedger.Domain.Export;

/// <summary>
/// Reproduces Go's strconv float formatting so exported numbers match the Go
/// reference byte-for-byte. Pure.
/// </summary>
public static class GoFloat
{
    /// <summary>
    /// Mirror of Go strconv.FormatFloat(v, 'f', -1, 64): shortest decimal that
    /// round-trips, never exponential. Used for XLSX numeric cell values.
    /// </summary>
    public static string FormatF(double v)
    {
        if (double.IsNaN(v))
        {
            return "NaN";
        }
        if (double.IsPositiveInfinity(v))
        {
            return "+Inf";
        }
        if (double.IsNegativeInfinity(v))
        {
            return "-Inf";
        }

        // "R" / shortest round-trip can yield exponential form (e.g. 1E-05).
        // Convert any exponential representation to plain decimal.
        string s = v.ToString("R", CultureInfo.InvariantCulture);
        if (s.IndexOf('E') < 0 && s.IndexOf('e') < 0)
        {
            return s;
        }
        return ExpandExponential(s);
    }

    /// <summary>
    /// Mirror of Go fmt.Sprint(float64) which uses strconv.FormatFloat(v, 'g',
    /// -1, 64): shortest decimal, exponential only for very large/small
    /// magnitudes. Used for CSV numeric fields.
    /// </summary>
    public static string FormatG(double v)
    {
        if (double.IsNaN(v))
        {
            return "NaN";
        }
        if (double.IsPositiveInfinity(v))
        {
            return "+Inf";
        }
        if (double.IsNegativeInfinity(v))
        {
            return "-Inf";
        }

        // Go 'g' switches to exponent when exp < -4 or exp >= 21 (shortest).
        string shortest = v.ToString("R", CultureInfo.InvariantCulture);
        // .NET "R" already produces shortest round-trip; its exponent threshold
        // differs from Go, so normalize to Go's rule.
        return NormalizeG(v, shortest);
    }

    private static string ExpandExponential(string s)
    {
        bool neg = false;
        int i = 0;
        if (s[0] == '-')
        {
            neg = true;
            i = 1;
        }
        else if (s[0] == '+')
        {
            i = 1;
        }

        int eIdx = s.IndexOfAny(new[] { 'E', 'e' }, i);
        string mantissa = s.Substring(i, eIdx - i);
        int exp = int.Parse(s[(eIdx + 1)..], CultureInfo.InvariantCulture);

        string intPart;
        string fracPart;
        int dot = mantissa.IndexOf('.');
        if (dot < 0)
        {
            intPart = mantissa;
            fracPart = "";
        }
        else
        {
            intPart = mantissa[..dot];
            fracPart = mantissa[(dot + 1)..];
        }

        string digits = intPart + fracPart;
        int pointPos = intPart.Length + exp;

        string result;
        if (pointPos <= 0)
        {
            result = "0." + new string('0', -pointPos) + digits;
        }
        else if (pointPos >= digits.Length)
        {
            result = digits + new string('0', pointPos - digits.Length);
        }
        else
        {
            result = digits[..pointPos] + "." + digits[pointPos..];
        }

        result = TrimDecimal(result);
        return neg ? "-" + result : result;
    }

    private static string TrimDecimal(string s)
    {
        if (s.IndexOf('.') < 0)
        {
            return s;
        }
        s = s.TrimEnd('0');
        if (s.EndsWith('.'))
        {
            s = s[..^1];
        }
        return s;
    }

    private static string NormalizeG(double v, string shortest)
    {
        if (v == 0)
        {
            return shortest;
        }

        // Determine decimal exponent of the shortest representation.
        string plain = shortest.IndexOfAny(new[] { 'E', 'e' }) >= 0 ? ExpandExponential(shortest) : shortest;
        string abs = plain.StartsWith('-') ? plain[1..] : plain;
        bool neg = plain.StartsWith('-');

        // Compute exponent: position of first significant digit relative to the
        // decimal point.
        int dot = abs.IndexOf('.');
        string intPart = dot < 0 ? abs : abs[..dot];
        string fracPart = dot < 0 ? "" : abs[(dot + 1)..];

        int exp;
        if (intPart != "0" && intPart.Length > 0)
        {
            exp = intPart.Length - 1;
        }
        else
        {
            // Leading zeros in fractional part.
            int lead = 0;
            while (lead < fracPart.Length && fracPart[lead] == '0')
            {
                lead++;
            }
            exp = -(lead + 1);
        }

        // Go 'g': use exponential when exp < -4 or exp >= 21.
        if (exp < -4 || exp >= 21)
        {
            return ToGoExponential(v, neg, intPart, fracPart, exp);
        }
        return plain;
    }

    private static string ToGoExponential(double v, bool neg, string intPart, string fracPart, int exp)
    {
        string digits = (intPart + fracPart).TrimStart('0');
        if (digits.Length == 0)
        {
            digits = "0";
        }
        digits = digits.TrimEnd('0');
        if (digits.Length == 0)
        {
            digits = "0";
        }

        string mantissa = digits.Length == 1
            ? digits
            : digits[..1] + "." + digits[1..];

        string sign = exp < 0 ? "-" : "+";
        string expStr = Math.Abs(exp).ToString("D2", CultureInfo.InvariantCulture);
        string body = mantissa + "e" + sign + expStr;
        return neg ? "-" + body : body;
    }
}
