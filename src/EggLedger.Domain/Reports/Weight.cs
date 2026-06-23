using System.Globalization;

namespace EggLedger.Domain.Reports;

/// <summary>
/// Heuristic query-cost classification for report definitions. Port of Go
/// reports/weight.go. Pure aside from the wall-clock used by date-window sizing.
/// </summary>
public static class Weight
{
    /// <summary>
    /// Assigns a cost tier ("LOW", "MEDIUM", "HEAVY") to a report definition.
    /// Port of Go ClassifyWeight.
    /// </summary>
    public static string ClassifyWeight(ReportDefinition def)
    {
        // Time-series + secondary group-by: time pivot (most expensive case first).
        if (def.SecondaryGroupBy != "" && def.Mode == "time_series")
        {
            var eitherIsArtifact = IsArtifactDimension(def.GroupBy) || IsArtifactDimension(def.SecondaryGroupBy);
            if (eitherIsArtifact)
            {
                if (HasDateFilter(def.Filters))
                {
                    return "MEDIUM";
                }
                return "HEAVY";
            }
            if (!HasDateFilter(def.Filters))
            {
                return "HEAVY";
            }
            if (DateFilterWindowDays(def.Filters) > 90)
            {
                return "HEAVY";
            }
            return "MEDIUM";
        }

        // Aggregate 2D pivot.
        if (def.SecondaryGroupBy != "")
        {
            var eitherIsArtifact = IsArtifactDimension(def.GroupBy) || IsArtifactDimension(def.SecondaryGroupBy);
            if (eitherIsArtifact)
            {
                if (HasDateFilter(def.Filters))
                {
                    return "MEDIUM";
                }
                return "HEAVY";
            }
            return "LOW";
        }

        // 1D time series.
        if (def.Mode == "time_series")
        {
            if (def.TimeBucket == "custom")
            {
                var days = CustomBucketDays(def.CustomBucketN, def.CustomBucketUnit);
                if (days > 90)
                {
                    return "HEAVY";
                }
                return "MEDIUM";
            }
            if (!HasDateFilter(def.Filters))
            {
                return "HEAVY";
            }
            var d = DateFilterWindowDays(def.Filters);
            if (d > 90)
            {
                return "HEAVY";
            }
            return "MEDIUM";
        }

        if (def.Mode == "aggregate" && HasArtifactScopeFilter(def.Filters))
        {
            return "MEDIUM";
        }
        return "LOW";
    }

    private static int CustomBucketDays(int n, string unit) => unit switch
    {
        "day" => n,
        "week" => n * 7,
        "month" => n * 30,
        _ => n,
    };

    private static bool HasDateFilter(ReportFilters f)
    {
        foreach (var c in f.And)
        {
            if (c.TopLevel == "launchDT" || c.TopLevel == "returnDT")
            {
                return true;
            }
        }
        foreach (var group in f.Or)
        {
            foreach (var c in group)
            {
                if (c.TopLevel == "launchDT" || c.TopLevel == "returnDT")
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Rough estimate of the filter window in days. Returns 9999 if it cannot be
    /// determined. Port of Go dateFilterWindowDays.
    /// </summary>
    private static int DateFilterWindowDays(ReportFilters f)
    {
        var minDays = 9999;
        var all = new List<FilterCondition>(f.And);
        foreach (var group in f.Or)
        {
            all.AddRange(group);
        }
        var now = DateTime.Now;
        foreach (var c in all)
        {
            if (c.TopLevel != "launchDT" && c.TopLevel != "returnDT")
            {
                continue;
            }
            if (c.Op != ">" && c.Op != ">=")
            {
                continue;
            }
            if (!DateTime.TryParseExact(c.Val, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var t))
            {
                continue;
            }
            var days = (int)((now - t).TotalHours / 24);
            if (days < minDays)
            {
                minDays = days;
            }
        }
        return minDays;
    }

    private static bool HasArtifactScopeFilter(ReportFilters f)
    {
        static bool IsArtifactField(string s) => s.StartsWith("artifact_", StringComparison.Ordinal);
        foreach (var c in f.And)
        {
            if (IsArtifactField(c.TopLevel))
            {
                return true;
            }
        }
        foreach (var group in f.Or)
        {
            foreach (var c in group)
            {
                if (IsArtifactField(c.TopLevel))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool IsArtifactDimension(string groupBy) => groupBy switch
    {
        "artifact_name" or "rarity" or "tier" or "spec_type" => true,
        _ => false,
    };
}
