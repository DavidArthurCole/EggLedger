using System.Globalization;
using EggLedger.Domain.Ei;
using EggLedger.Domain.LedgerData;
using Ei;

namespace EggLedger.Domain.Reports;

/// <summary>
/// Raw-value to display-string conversion and sort ordering for report group-by dimensions.
/// Port of Go reports/labels.go.
/// </summary>
public static class Labels
{
    private static readonly string[] RarityNames = ["Common", "Rare", "Epic", "Legendary"];
    private static readonly string[] TierNames = ["T1", "T2", "T3", "T4"];

    private static LedgerDisplayData Config => LedgerData.LedgerData.Config;

    /// <summary>
    /// Display name for an artifact enum value via ledgerdata.ArtifactTargets,
    /// falling back to CasedName. Port of Go artifactDisplayName.
    /// </summary>
    private static string ArtifactDisplayName(int v)
    {
        var protoName = EnumNames.ProtoName((ArtifactSpec.Name)v);
        foreach (var t in Config.ArtifactTargets)
        {
            if (t.Name == protoName)
            {
                return t.DisplayName;
            }
        }
        return ((ArtifactSpec.Name)v).CasedName();
    }

    /// <summary>GROUP BY dimensions whose raw SQL value is an integer. Port of Go numericGroupBys.</summary>
    private static readonly HashSet<string> NumericGroupBys = new(StringComparer.Ordinal)
    {
        "ship_type", "duration_type", "level", "mission_type",
        "rarity", "tier", "artifact_name", "mission_target",
    };

    /// <summary>
    /// True when rawA sorts before rawB for the groupBy. Numeric dimensions compare
    /// by integer, others ordinal. Port of Go LabelSortLess.
    /// </summary>
    public static bool LabelSortLess(string groupBy, string rawA, string rawB)
    {
        if (NumericGroupBys.Contains(groupBy))
        {
            var okA = long.TryParse(rawA, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var a);
            var okB = long.TryParse(rawB, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var b);
            if (okA && okB)
            {
                return a < b;
            }
        }
        return string.CompareOrdinal(rawA, rawB) < 0;
    }

    /// <summary>Converts a raw SQL GROUP BY value to a display string. Port of Go FormatLabel.</summary>
    public static string FormatLabel(string groupBy, string rawVal)
    {
        int ParseInt()
        {
            int.TryParse(rawVal, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v);
            return v;
        }

        switch (groupBy)
        {
            case "ship_type":
                return ((MissionInfo.Spaceship)ParseInt()).Name();
            case "duration_type":
                return ((MissionInfo.DurationType)ParseInt()).Display();
            case "level":
                return $"Level {rawVal}";
            case "mission_type":
                return ((MissionInfo.MissionType)ParseInt()).Display();
            case "mission_target":
                var v = ParseInt();
                if (v < 0)
                {
                    return "None (Pre 1.27)";
                }
                if (v == 0)
                {
                    return "Untargeted";
                }
                return ArtifactDisplayName(v);
            case "artifact_name":
                return ArtifactDisplayName(ParseInt());
            case "rarity":
                var ri = ParseInt();
                if (ri >= 0 && ri < RarityNames.Length)
                {
                    return RarityNames[ri];
                }
                return rawVal;
            case "tier":
                var ti = ParseInt();
                if (ti >= 0 && ti < TierNames.Length)
                {
                    return TierNames[ti];
                }
                return rawVal;
            case "spec_type":
                return rawVal;
            case "time_bucket":
                return rawVal;
        }
        return rawVal;
    }
}
