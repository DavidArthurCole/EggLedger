using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EggLedger.Domain.Util;

/// <summary>
/// Pure version parse + compare, ported to match hashicorp/go-version v1.6.0
/// (EggLedger/update/version.go uses version.NewVersion + GreaterThan/LessThan).
/// Lives in Domain because it is pure and UI-agnostic; only desktop self-updates,
/// but the compare itself has no host dependency.
///
/// Semantics matched exactly to go-version v1.6.0:
/// - optional leading "v"; numeric segments padded to at least 3 with zeros
///   (1.2 == 1.2.0).
/// - prerelease after "-", metadata after "+".
/// - build metadata is ignored in comparison.
/// - a version WITH a prerelease is less than the same core WITHOUT one
///   (1.0.0-alpha &lt; 1.0.0).
/// - prerelease compared dot-part by dot-part: numeric part &lt; non-numeric part;
///   both numeric -&gt; integer compare; both non-numeric -&gt; ordinal string compare;
///   a missing part defers to the present part's kind.
/// </summary>
public sealed partial class SemVersion : IComparable<SemVersion>, IEquatable<SemVersion>
{
    // Mirrors go-version v1.6.0 VersionRegexpRaw (the non-strict NewVersion form).
    [GeneratedRegex(
        @"^v?([0-9]+(\.[0-9]+)*?)" +
        @"(-([0-9]+[0-9A-Za-z\-~]*(\.[0-9A-Za-z\-~]+)*)|(-?([A-Za-z\-~]+[0-9A-Za-z\-~]*(\.[0-9A-Za-z\-~]+)*)))?" +
        @"(\+([0-9A-Za-z\-~]+(\.[0-9A-Za-z\-~]+)*))?" +
        @"?$")]
    private static partial Regex VersionRegex();

    private readonly long[] _segments;
    private readonly string _pre;
    private readonly string _metadata;
    private readonly string _original;

    private SemVersion(long[] segments, string pre, string metadata, string original)
    {
        _segments = segments;
        _pre = pre;
        _metadata = metadata;
        _original = original;
    }

    /// <summary>Numeric segments (padded to at least 3), excluding pre/metadata.</summary>
    public IReadOnlyList<long> Segments => _segments;

    /// <summary>Prerelease string (after "-"), or empty.</summary>
    public string Prerelease => _pre;

    /// <summary>Metadata string (after "+"), or empty.</summary>
    public string Metadata => _metadata;

    /// <summary>The original string as passed to Parse.</summary>
    public string Original => _original;

    /// <summary>
    /// Parse a version, matching go-version NewVersion. Returns false on a
    /// malformed version (the Go path returns an error and the caller bails).
    /// </summary>
    public static bool TryParse(string? input, out SemVersion? version)
    {
        version = null;
        if (input is null)
        {
            return false;
        }

        var match = VersionRegex().Match(input);
        if (!match.Success)
        {
            return false;
        }

        // matches[1] in Go == group 1 here: the dotted numeric core.
        var segmentsStr = match.Groups[1].Value.Split('.');
        var segments = new long[segmentsStr.Length];
        for (var i = 0; i < segmentsStr.Length; i++)
        {
            if (!long.TryParse(segmentsStr[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var val))
            {
                return false;
            }
            segments[i] = val;
        }

        // Pad to at least 3 segments with zeros (go-version basic semver default).
        if (segments.Length < 3)
        {
            var padded = new long[3];
            Array.Copy(segments, padded, segments.Length);
            segments = padded;
        }

        // Go: pre := matches[7]; if pre == "" { pre = matches[4] }.
        var pre = match.Groups[7].Value;
        if (pre.Length == 0)
        {
            pre = match.Groups[4].Value;
        }

        // Go: metadata := matches[10].
        var metadata = match.Groups[10].Value;

        version = new SemVersion(segments, pre, metadata, input);
        return true;
    }

    /// <summary>Parse or throw (mirrors usage where the version is known-good).</summary>
    public static SemVersion Parse(string input)
        => TryParse(input, out var v) && v is not null
            ? v
            : throw new FormatException($"Malformed version: {input}");

    /// <summary>
    /// Canonical string: dotted segments, then "-pre", then "+metadata".
    /// Matches go-version String() (used for the fast-path equality check).
    /// </summary>
    public string Canonical()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < _segments.Length; i++)
        {
            if (i > 0)
            {
                sb.Append('.');
            }
            sb.Append(_segments[i].ToString(CultureInfo.InvariantCulture));
        }
        if (_pre.Length > 0)
        {
            sb.Append('-').Append(_pre);
        }
        if (_metadata.Length > 0)
        {
            sb.Append('+').Append(_metadata);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Compare this to other: -1 if less, 0 if equal, 1 if greater. Ports
    /// go-version Version.Compare exactly.
    /// </summary>
    public int CompareTo(SemVersion? other)
    {
        ArgumentNullException.ThrowIfNull(other);

        // Fast canonical-string equality (go-version short-circuit).
        if (Canonical() == other.Canonical())
        {
            return 0;
        }

        var self = _segments;
        var oth = other._segments;

        if (SegmentsEqual(self, oth))
        {
            var preSelf = _pre;
            var preOther = other._pre;
            if (preSelf.Length == 0 && preOther.Length == 0)
            {
                return 0;
            }
            if (preSelf.Length == 0)
            {
                return 1;
            }
            if (preOther.Length == 0)
            {
                return -1;
            }
            return ComparePrereleases(preSelf, preOther);
        }

        var lenSelf = self.Length;
        var lenOther = oth.Length;
        var hS = Math.Max(lenSelf, lenOther);
        for (var i = 0; i < hS; i++)
        {
            if (i > lenSelf - 1)
            {
                if (!AllZero(oth, i))
                {
                    return -1;
                }
                break;
            }
            if (i > lenOther - 1)
            {
                if (!AllZero(self, i))
                {
                    return 1;
                }
                break;
            }
            var lhs = self[i];
            var rhs = oth[i];
            if (lhs == rhs)
            {
                continue;
            }
            return lhs < rhs ? -1 : 1;
        }

        return 0;
    }

    public bool GreaterThan(SemVersion other) => CompareTo(other) > 0;
    public bool LessThan(SemVersion other) => CompareTo(other) < 0;
    public bool GreaterThanOrEqual(SemVersion other) => CompareTo(other) >= 0;
    public bool LessThanOrEqual(SemVersion other) => CompareTo(other) <= 0;

    public bool Equals(SemVersion? other) => other is not null && CompareTo(other) == 0;
    public override bool Equals(object? obj) => obj is SemVersion v && Equals(v);

    public override int GetHashCode()
    {
        // Equality ignores metadata and uses padded segments + prerelease; hash to
        // match (canonical without metadata).
        var hash = new HashCode();
        foreach (var s in _segments)
        {
            hash.Add(s);
        }
        hash.Add(_pre);
        return hash.ToHashCode();
    }

    public override string ToString() => Canonical();

    private static bool SegmentsEqual(long[] a, long[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }
        for (var i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }
        return true;
    }

    private static bool AllZero(long[] segs, int from)
    {
        for (var i = from; i < segs.Length; i++)
        {
            if (segs[i] != 0)
            {
                return false;
            }
        }
        return true;
    }

    // Ports go-version comparePrereleases: split on ".", compare each part.
    private static int ComparePrereleases(string v, string other)
    {
        if (v == other)
        {
            return 0;
        }

        var selfParts = v.Split('.');
        var otherParts = other.Split('.');
        var biggest = Math.Max(selfParts.Length, otherParts.Length);

        for (var i = 0; i < biggest; i++)
        {
            var partSelf = i < selfParts.Length ? selfParts[i] : "";
            var partOther = i < otherParts.Length ? otherParts[i] : "";
            var c = ComparePart(partSelf, partOther);
            if (c != 0)
            {
                return c;
            }
        }
        return 0;
    }

    // Ports go-version comparePart.
    private static int ComparePart(string preSelf, string preOther)
    {
        if (preSelf == preOther)
        {
            return 0;
        }

        var selfNumeric = long.TryParse(preSelf, NumberStyles.Integer, CultureInfo.InvariantCulture, out var selfInt);
        var otherNumeric = long.TryParse(preOther, NumberStyles.Integer, CultureInfo.InvariantCulture, out var otherInt);

        if (preSelf.Length == 0)
        {
            return otherNumeric ? -1 : 1;
        }
        if (preOther.Length == 0)
        {
            return selfNumeric ? 1 : -1;
        }

        if (selfNumeric && !otherNumeric)
        {
            return -1;
        }
        if (!selfNumeric && otherNumeric)
        {
            return 1;
        }
        if (!selfNumeric && !otherNumeric && string.CompareOrdinal(preSelf, preOther) > 0)
        {
            return 1;
        }
        if (selfNumeric && otherNumeric && selfInt > otherInt)
        {
            return 1;
        }
        return -1;
    }
}
