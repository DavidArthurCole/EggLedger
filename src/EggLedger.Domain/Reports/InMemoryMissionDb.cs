using System.Globalization;

namespace EggLedger.Domain.Reports;

/// <summary>
/// In-memory <see cref="IMissionDb"/> that answers the queries
/// <see cref="ReportExecutor"/> issues by evaluating the filter and grouping
/// semantics of Go reports/query.go directly over typed rows, with no SQL engine.
///
/// It does not parse arbitrary SQL. Every query ReportExecutor emits for a given
/// ReportDefinition is one of a fixed set of shapes; this class recognizes each
/// shape from stable markers in the generated text (the same technique the A7
/// FakeDb uses) and computes the result columns the executor expects to Scan.
/// Returned rows mirror the SQL Scan order and boxing: text columns as string,
/// counts as long, cap_weight/airtime sums as double.
///
/// COUPLED SOURCE OF TRUTH: the marker literals below are the exact SELECT/FROM
/// aliases and JOIN text that <see cref="QueryBuilder"/> and
/// <see cref="ReportExecutor"/> emit. If either changes an alias, the JOIN line,
/// or the airtime SUM expression, update the matching const here in lockstep.
/// </summary>
internal sealed class InMemoryMissionDb : IMissionDb
{
    // Markers identifying each query shape ReportExecutor/QueryBuilder emit. Keep
    // in sync with those types (see class summary).
    private const string CapWeightMarker = "cap_weight";
    private const string BucketMarker = "AS bucket";
    private const string GrpMarker = "AS grp";
    private const string AirtimeSumMarker = "SUM(CAST(m.return_timestamp - m.start_timestamp AS REAL) / 3600.0)";

    // Present only when the artifact_drops JOIN is in the FROM clause. A count or
    // denom query that is FROM mission m has neither marker, so it groups over
    // mission rows; a value query that joins artifact_drops has both.
    private const string ArtifactJoinMarker = "JOIN mission m ON d.mission_id = m.mission_id";
    private const string CountMarker = "COUNT(*)";
    private const string GroupByMarker = "GROUP BY ";

    private readonly ReportDefinition _def;
    private readonly List<MissionRowData> _missions;
    private readonly List<ArtifactDropRowData> _drops;
    private readonly IWeightData _weights;

    // Drops keyed by (player, mission) for EXISTS-subquery evaluation.
    private readonly ILookup<(string Player, string Mission), ArtifactDropRowData> _dropsByMission;

    public InMemoryMissionDb(
        ReportDefinition def,
        IReadOnlyList<MissionRowData> missions,
        IReadOnlyList<ArtifactDropRowData> drops,
        IWeightData weights)
    {
        _def = def;
        _missions = missions.ToList();
        _drops = drops.ToList();
        _weights = weights;
        _dropsByMission = _drops.ToLookup(d => (d.PlayerId, d.MissionId));
    }

    public IReadOnlyList<object?[]> Query(string sql, IReadOnlyList<object?> args)
    {
        var weighted = sql.Contains(CapWeightMarker, StringComparison.Ordinal);
        var hasBucket = sql.Contains(BucketMarker, StringComparison.Ordinal);
        var hasGrp = sql.Contains(GrpMarker, StringComparison.Ordinal);
        var airtimeDenom = sql.Contains(AirtimeSumMarker, StringComparison.Ordinal);

        // Whether this query joins artifact_drops. Count/denom queries that are
        // FROM mission m do NOT join and must group over mission rows; value queries
        // that aggregate drops do. Dispatch on the SQL shape, not on _def.Subject:
        // a family-weighted report has Subject "artifacts" yet still issues a
        // FROM-mission mission-count query that must count missions, not drops.
        var joinDrops = sql.Contains(ArtifactJoinMarker, StringComparison.Ordinal);

        if (weighted)
        {
            if (hasBucket && hasGrp)
            {
                return WeightedTimePivot();
            }
            if (hasBucket)
            {
                return WeightedTimeSeries();
            }
            if (_def.SecondaryGroupBy != "")
            {
                return WeightedPivot();
            }
            return WeightedAggregate();
        }

        if (hasBucket && hasGrp)
        {
            return TimePivotCount(joinDrops);
        }
        if (hasBucket)
        {
            return TimeSeriesCount(joinDrops);
        }

        // Remaining shapes select COUNT(*) or an airtime SUM grouped by one or two
        // plain columns. The executor's main aggregate ("ORDER BY count DESC"), the
        // pivot count, and the denom/mission-count helpers all land here; they are
        // distinguished by dimensionality, airtime vs count, and whether the drops
        // JOIN is present, which is all the result assembly cares about.
        if (airtimeDenom)
        {
            return _def.SecondaryGroupBy != "" && Is2DAirtimeQuery(sql)
                ? Airtime2D(joinDrops)
                : Airtime1D(joinDrops);
        }

        // Plain counts: one or two group columns. Guard the fall-through so an alias
        // or spacing change in QueryBuilder fails loudly instead of mis-routing.
        if (!sql.Contains(CountMarker, StringComparison.Ordinal)
            || !sql.Contains(GroupByMarker, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"unrecognized query shape: {sql}");
        }
        return Is2DCountQuery(sql) ? Count2D(joinDrops) : Count1D(joinDrops);
    }

    // A 2D count/airtime helper query groups by both dimension columns. The
    // executor builds these (pivot, denom2D, mission-count) with both GroupByColumn
    // text casts present in the SELECT list.
    private bool Is2DCountQuery(string sql)
    {
        if (_def.SecondaryGroupBy == "")
        {
            return false;
        }
        var col1 = QueryBuilder.GroupByColumn(_def.GroupBy);
        var col2 = QueryBuilder.GroupByColumn(_def.SecondaryGroupBy);
        return col1 != "" && col2 != ""
            && sql.Contains("CAST(" + col1 + " AS TEXT), CAST(" + col2 + " AS TEXT)", StringComparison.Ordinal);
    }

    private bool Is2DAirtimeQuery(string sql)
    {
        if (_def.SecondaryGroupBy == "")
        {
            return false;
        }
        var col2 = QueryBuilder.GroupByColumn(_def.SecondaryGroupBy);
        return col2 != "" && sql.Contains("CAST(" + col2 + " AS TEXT), SUM", StringComparison.Ordinal);
    }

    // Missions passing the player filter and the report filter conditions.
    private IEnumerable<MissionRowData> FilteredMissions() =>
        _missions.Where(m => m.PlayerId == _def.AccountId && PassesFilters(m));

    // (mission, drop) pairs for artifact-subject queries: JOIN + d.drop_index >= 0.
    private IEnumerable<(MissionRowData M, ArtifactDropRowData D)> FilteredJoin()
    {
        foreach (var m in FilteredMissions())
        {
            foreach (var d in _dropsByMission[(m.PlayerId, m.MissionId)])
            {
                if (d.DropIndex >= 0 && PassesArtifactFilters(d))
                {
                    yield return (m, d);
                }
            }
        }
    }

    private List<object?[]> Count1D(bool joinDrops)
    {
        var col = QueryBuilder.GroupByColumn(_def.GroupBy);
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var key in GroupKeys1D(col, joinDrops))
        {
            if (!counts.ContainsKey(key))
            {
                order.Add(key);
            }
            counts.TryGetValue(key, out var cur);
            counts[key] = cur + 1;
        }
        // The main aggregate query orders by count DESC (stable on first-seen). The
        // denom helper has no ORDER BY; the executor only builds a lookup map from
        // it, so order is irrelevant there. Emitting count-desc is safe for both.
        var rows = order
            .Select((k, i) => (k, i, c: counts[k]))
            .OrderByDescending(x => x.c)
            .ThenBy(x => x.i)
            .Select(x => new object?[] { x.k, x.c })
            .ToList();
        return rows;
    }

    private List<object?[]> Count2D(bool joinDrops)
    {
        var col1 = QueryBuilder.GroupByColumn(_def.GroupBy);
        var col2 = QueryBuilder.GroupByColumn(_def.SecondaryGroupBy);
        var counts = new Dictionary<(string, string), long>();
        var order = new List<(string, string)>();
        foreach (var (k1, k2) in GroupKeys2D(col1, col2, joinDrops))
        {
            var key = (k1, k2);
            if (!counts.ContainsKey(key))
            {
                order.Add(key);
            }
            counts.TryGetValue(key, out var cur);
            counts[key] = cur + 1;
        }
        // Pivot queries order by col1, col2 ascending. PivotAccum re-sorts axes by
        // raw value, so the row order here does not affect the matrix; emit ascending
        // for fidelity.
        var rows = order
            .OrderBy(k => k, KeyPairComparer(col1, col2))
            .Select(k => new object?[] { k.Item1, k.Item2, counts[k] })
            .ToList();
        return rows;
    }

    // Airtime denom queries are always FROM mission m (no drops join), so they sum
    // over mission rows. joinDrops is threaded for shape consistency and will be
    // false for every airtime query the executor emits.
    private List<object?[]> Airtime1D(bool joinDrops)
    {
        var col = QueryBuilder.GroupByColumn(_def.GroupBy);
        var sums = new Dictionary<string, double>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var m in AirtimeRows(joinDrops, col, null))
        {
            var key = ColValue(m, col);
            if (!sums.ContainsKey(key))
            {
                order.Add(key);
            }
            sums.TryGetValue(key, out var cur);
            sums[key] = cur + ((m.ReturnTimestamp - m.StartTimestamp) / 3600.0);
        }
        return order.Select(k => new object?[] { k, sums[k] }).ToList();
    }

    private List<object?[]> Airtime2D(bool joinDrops)
    {
        var col1 = QueryBuilder.GroupByColumn(_def.GroupBy);
        var col2 = QueryBuilder.GroupByColumn(_def.SecondaryGroupBy);
        var sums = new Dictionary<(string, string), double>();
        var order = new List<(string, string)>();
        foreach (var m in AirtimeRows(joinDrops, col1, col2))
        {
            var key = (ColValue(m, col1), ColValue(m, col2));
            if (!sums.ContainsKey(key))
            {
                order.Add(key);
            }
            sums.TryGetValue(key, out var cur);
            sums[key] = cur + ((m.ReturnTimestamp - m.StartTimestamp) / 3600.0);
        }
        return order.Select(k => new object?[] { k.Item1, k.Item2, sums[k] }).ToList();
    }

    // Mission rows for an airtime SUM. When the query joins artifact_drops the sum
    // is over (mission, drop) pairs (a mission contributes once per drop); otherwise
    // over distinct missions.
    private IEnumerable<MissionRowData> AirtimeRows(bool joinDrops, string col1, string? col2)
    {
        if (joinDrops
            || col1.StartsWith("d.", StringComparison.Ordinal)
            || (col2 != null && col2.StartsWith("d.", StringComparison.Ordinal)))
        {
            return FilteredJoin().Select(p => p.M);
        }
        return FilteredMissions();
    }

    private List<object?[]> TimeSeriesCount(bool joinDrops)
    {
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var m in FilteredBucketRows(joinDrops))
        {
            var bucket = BucketLabel(m.StartTimestamp);
            counts.TryGetValue(bucket, out var cur);
            counts[bucket] = cur + 1;
        }
        return counts
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new object?[] { kv.Key, kv.Value })
            .ToList();
    }

    private List<object?[]> TimePivotCount(bool joinDrops)
    {
        var col2 = QueryBuilder.GroupByColumn(_def.SecondaryGroupBy);
        var counts = new Dictionary<(string, string), long>();
        foreach (var (m, d) in FilteredBucketJoin(col2, joinDrops))
        {
            var bucket = BucketLabel(m.StartTimestamp);
            var grp = col2.StartsWith("d.", StringComparison.Ordinal) ? ColValueDrop(d!, col2) : ColValue(m, col2);
            var key = (bucket, grp);
            counts.TryGetValue(key, out var cur);
            counts[key] = cur + 1;
        }
        return counts
            .OrderBy(kv => kv.Key.Item1, StringComparer.Ordinal)
            .ThenBy(kv => kv.Key.Item2, StringComparer.Ordinal)
            .Select(kv => new object?[] { kv.Key.Item1, kv.Key.Item2, kv.Value })
            .ToList();
    }

    private List<object?[]> WeightedAggregate()
    {
        var col = QueryBuilder.GroupByColumn(_def.GroupBy);
        // GROUP BY groupCol, d.artifact_id, d.level; SUM cap_weight; order cap DESC.
        var groups = new Dictionary<(string, long, long), double>();
        var order = new List<(string, long, long)>();
        foreach (var (m, d) in WeightedJoin())
        {
            var key = (col.StartsWith("d.", StringComparison.Ordinal) ? ColValueDrop(d, col) : ColValue(m, col), d.ArtifactId, d.Level);
            if (!groups.ContainsKey(key))
            {
                order.Add(key);
            }
            groups.TryGetValue(key, out var cur);
            groups[key] = cur + CapWeight(m);
        }
        var rows = order
            .Select(k => (k, cap: groups[k]))
            .OrderByDescending(x => x.cap)
            .Select(x => new object?[] { x.k.Item1, x.k.Item2, x.k.Item3, x.cap })
            .ToList();
        return rows;
    }

    private List<object?[]> WeightedPivot()
    {
        var col1 = QueryBuilder.GroupByColumn(_def.GroupBy);
        var col2 = QueryBuilder.GroupByColumn(_def.SecondaryGroupBy);
        var groups = new Dictionary<(string, string, long, long), double>();
        var order = new List<(string, string, long, long)>();
        foreach (var (m, d) in WeightedJoin())
        {
            var k1 = col1.StartsWith("d.", StringComparison.Ordinal) ? ColValueDrop(d, col1) : ColValue(m, col1);
            var k2 = col2.StartsWith("d.", StringComparison.Ordinal) ? ColValueDrop(d, col2) : ColValue(m, col2);
            var key = (k1, k2, d.ArtifactId, d.Level);
            if (!groups.ContainsKey(key))
            {
                order.Add(key);
            }
            groups.TryGetValue(key, out var cur);
            groups[key] = cur + CapWeight(m);
        }
        // Order by col1, col2 ascending (raw). PivotAccum re-sorts, so order is moot.
        var rows = order
            .Select(k => new object?[] { k.Item1, k.Item2, k.Item3, k.Item4, groups[k] })
            .ToList();
        return rows;
    }

    private List<object?[]> WeightedTimeSeries()
    {
        var groups = new Dictionary<(string, long, long), double>();
        var order = new List<(string, long, long)>();
        foreach (var (m, d) in WeightedBucketJoin())
        {
            var key = (BucketLabel(m.StartTimestamp), d.ArtifactId, d.Level);
            if (!groups.ContainsKey(key))
            {
                order.Add(key);
            }
            groups.TryGetValue(key, out var cur);
            groups[key] = cur + CapWeight(m);
        }
        // ORDER BY bucket ASC. The executor accumulates per bucket; first-seen order
        // determines the float-series order, so emit bucket-ascending.
        var rows = order
            .OrderBy(k => k.Item1, StringComparer.Ordinal)
            .Select(k => new object?[] { k.Item1, k.Item2, k.Item3, groups[k] })
            .ToList();
        return rows;
    }

    private List<object?[]> WeightedTimePivot()
    {
        var col2 = QueryBuilder.GroupByColumn(_def.SecondaryGroupBy);
        var groups = new Dictionary<(string, string, long, long), double>();
        var order = new List<(string, string, long, long)>();
        foreach (var (m, d) in WeightedBucketJoin())
        {
            var grp = col2.StartsWith("d.", StringComparison.Ordinal) ? ColValueDrop(d, col2) : ColValue(m, col2);
            var key = (BucketLabel(m.StartTimestamp), grp, d.ArtifactId, d.Level);
            if (!groups.ContainsKey(key))
            {
                order.Add(key);
            }
            groups.TryGetValue(key, out var cur);
            groups[key] = cur + CapWeight(m);
        }
        var rows = order
            .OrderBy(k => k.Item1, StringComparer.Ordinal)
            .ThenBy(k => k.Item2, StringComparer.Ordinal)
            .Select(k => new object?[] { k.Item1, k.Item2, k.Item3, k.Item4, groups[k] })
            .ToList();
        return rows;
    }

    // Group keys for a 1D non-time query. joinDrops reflects whether the SQL joins
    // artifact_drops (per-drop counting) or is FROM mission m (per-mission counting).
    private IEnumerable<string> GroupKeys1D(string col, bool joinDrops)
    {
        if (joinDrops || col.StartsWith("d.", StringComparison.Ordinal))
        {
            foreach (var (m, d) in FilteredJoin())
            {
                yield return col.StartsWith("d.", StringComparison.Ordinal) ? ColValueDrop(d, col) : ColValue(m, col);
            }
            yield break;
        }
        foreach (var m in FilteredMissions())
        {
            yield return ColValue(m, col);
        }
    }

    private IEnumerable<(string, string)> GroupKeys2D(string col1, string col2, bool joinDrops)
    {
        var needJoin = joinDrops
            || col1.StartsWith("d.", StringComparison.Ordinal)
            || col2.StartsWith("d.", StringComparison.Ordinal);
        if (needJoin)
        {
            foreach (var (m, d) in FilteredJoin())
            {
                var k1 = col1.StartsWith("d.", StringComparison.Ordinal) ? ColValueDrop(d, col1) : ColValue(m, col1);
                var k2 = col2.StartsWith("d.", StringComparison.Ordinal) ? ColValueDrop(d, col2) : ColValue(m, col2);
                yield return (k1, k2);
            }
            yield break;
        }
        foreach (var m in FilteredMissions())
        {
            yield return (ColValue(m, col1), ColValue(m, col2));
        }
    }

    // Time-series source rows, honoring the drops JOIN and the custom window.
    private IEnumerable<MissionRowData> FilteredBucketRows(bool joinDrops)
    {
        if (joinDrops)
        {
            return FilteredJoin().Where(p => InCustomWindow(p.M)).Select(p => p.M);
        }
        return FilteredMissions().Where(InCustomWindow);
    }

    private IEnumerable<(MissionRowData M, ArtifactDropRowData? D)> FilteredBucketJoin(string col2, bool joinDrops)
    {
        var needJoin = joinDrops || col2.StartsWith("d.", StringComparison.Ordinal);
        if (needJoin)
        {
            foreach (var (m, d) in FilteredJoin())
            {
                if (InCustomWindow(m))
                {
                    yield return (m, d);
                }
            }
            yield break;
        }
        foreach (var m in FilteredMissions().Where(InCustomWindow))
        {
            yield return (m, null);
        }
    }

    // Weighted (family) joins always use the artifact source and apply the family
    // afx-id filter (d.artifact_id IN (...)).
    private IEnumerable<(MissionRowData M, ArtifactDropRowData D)> WeightedJoin()
    {
        var family = new HashSet<long>(_weights.FamilyAfxIds(_def.FamilyWeight).Select(i => (long)i));
        foreach (var (m, d) in FilteredJoin())
        {
            if (family.Contains(d.ArtifactId))
            {
                yield return (m, d);
            }
        }
    }

    private IEnumerable<(MissionRowData M, ArtifactDropRowData D)> WeightedBucketJoin() =>
        WeightedJoin().Where(p => InCustomWindow(p.M));

    private double CapWeight(MissionRowData m) =>
        m.NominalCapacity > 0 && m.Capacity > 0
            ? (double)m.NominalCapacity / m.Capacity
            : 1.0;

    private Comparer<(string, string)> KeyPairComparer(string col1, string col2) =>
        Comparer<(string A, string B)>.Create((x, y) =>
        {
            var c = CompareCol(col1, x.A, y.A);
            return c != 0 ? c : CompareCol(col2, x.B, y.B);
        });

    // SQLite orders integer columns numerically and text columns lexicographically.
    private static int CompareCol(string col, string a, string b)
    {
        if (!col.EndsWith("spec_type", StringComparison.Ordinal)
            && long.TryParse(a, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var ia)
            && long.TryParse(b, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var ib))
        {
            return ia.CompareTo(ib);
        }
        return string.CompareOrdinal(a, b);
    }

    private string BucketLabel(long unixSeconds) =>
        TimeBucket.Format(_def.TimeBucket, _def.CustomBucketUnit, unixSeconds);

    // Custom "last N units" window from the start_timestamp. Non-custom buckets
    // have no window. Mirrors CustomWindowCondition + strftime('%s','now',modifier).
    private bool InCustomWindow(MissionRowData m)
    {
        if (_def.TimeBucket != "custom")
        {
            return true;
        }
        var (cond, modifier) = QueryBuilder.CustomWindowCondition(_def.CustomBucketN, _def.CustomBucketUnit);
        if (cond == "" || modifier is not string mod)
        {
            return true;
        }
        var cutoff = TimeBucket.NowMinus(mod);
        return m.StartTimestamp >= cutoff;
    }

    private static string ColValue(MissionRowData m, string col) => col switch
    {
        "m.ship" => m.Ship.ToString(CultureInfo.InvariantCulture),
        "m.duration_type" => m.DurationType.ToString(CultureInfo.InvariantCulture),
        "m.level" => m.Level.ToString(CultureInfo.InvariantCulture),
        "m.mission_type" => m.MissionType.ToString(CultureInfo.InvariantCulture),
        "m.target" => m.Target.ToString(CultureInfo.InvariantCulture),
        _ => "",
    };

    private static string ColValueDrop(ArtifactDropRowData d, string col) => col switch
    {
        "d.artifact_id" => d.ArtifactId.ToString(CultureInfo.InvariantCulture),
        "d.rarity" => d.Rarity.ToString(CultureInfo.InvariantCulture),
        "d.level" => d.Level.ToString(CultureInfo.InvariantCulture),
        "d.spec_type" => d.SpecType,
        _ => "",
    };

    // Mission-scope filter evaluation (the m.* conditions of BuildWhereClause).
    private bool PassesFilters(MissionRowData m)
    {
        foreach (var c in _def.Filters.And)
        {
            if (!EvalMission(c, m))
            {
                return false;
            }
        }
        foreach (var group in _def.Filters.Or)
        {
            var any = false;
            var hadClause = false;
            foreach (var c in group)
            {
                if (!IsMissionScope(c))
                {
                    continue;
                }
                hadClause = true;
                if (EvalMission(c, m))
                {
                    any = true;
                }
            }
            if (hadClause && !any)
            {
                return false;
            }
        }
        return true;
    }

    // Artifact-scope (d.*) filter evaluation, applied per drop row in the JOIN.
    private bool PassesArtifactFilters(ArtifactDropRowData d)
    {
        foreach (var c in _def.Filters.And)
        {
            if (IsArtifactScope(c) && !EvalArtifact(c, d))
            {
                return false;
            }
        }
        foreach (var group in _def.Filters.Or)
        {
            var any = false;
            var hadClause = false;
            foreach (var c in group)
            {
                if (!IsArtifactScope(c))
                {
                    continue;
                }
                hadClause = true;
                if (EvalArtifact(c, d))
                {
                    any = true;
                }
            }
            if (hadClause && !any)
            {
                return false;
            }
        }
        return true;
    }

    private static readonly HashSet<string> ArtifactTopLevels = new(StringComparer.Ordinal)
    {
        "artifact_rarity", "artifact_spec_type", "artifact_name", "artifact_tier", "artifact_quality",
    };

    private static bool IsArtifactScope(FilterCondition c) => ArtifactTopLevels.Contains(c.TopLevel);

    private static bool IsMissionScope(FilterCondition c) => !IsArtifactScope(c);

    // Evaluates a mission-scope condition. Artifact-scope and no-op conditions
    // (those QueryBuilder.ConditionToSql would drop) return true here so they do
    // not exclude a mission on the m.* pass.
    private bool EvalMission(FilterCondition c, MissionRowData m)
    {
        switch (c.TopLevel)
        {
            case "dubcap":
                return m.IsDubCap == (c.Op == "true");
            case "buggedcap":
                return m.IsBuggedCap == (c.Op == "true");
            case "drops":
                return EvalDrops(c, m);
            case "launchDT":
                return EvalDate(c, m.StartTimestamp);
            case "returnDT":
                return EvalDate(c, m.ReturnTimestamp);
            case "ship":
                return CompareNumeric(c, m.Ship);
            case "duration":
                return CompareNumeric(c, m.DurationType);
            case "level":
                return CompareNumeric(c, m.Level);
            case "target":
                return CompareNumeric(c, m.Target);
            case "type":
                return CompareNumeric(c, m.MissionType);
            default:
                return true;
        }
    }

    private bool EvalArtifact(FilterCondition c, ArtifactDropRowData d)
    {
        // Only comparison ops are valid; ConditionToSql rejects others as no-ops.
        if (c.Op is not ("=" or "!=" or ">" or "<" or ">=" or "<="))
        {
            return true;
        }
        switch (c.TopLevel)
        {
            case "artifact_rarity":
                return IsInt(c.Val) && CompareLong(c.Op, d.Rarity, long.Parse(c.Val, CultureInfo.InvariantCulture));
            case "artifact_tier":
                return IsInt(c.Val) && CompareLong(c.Op, d.Level, long.Parse(c.Val, CultureInfo.InvariantCulture));
            case "artifact_name":
                return IsInt(c.Val) && CompareLong(c.Op, d.ArtifactId, long.Parse(c.Val, CultureInfo.InvariantCulture));
            case "artifact_quality":
                return IsFloat(c.Val) && CompareDouble(c.Op, d.Quality, double.Parse(c.Val, CultureInfo.InvariantCulture));
            case "artifact_spec_type":
                return CompareText(c.Op, d.SpecType, c.Val);
            default:
                return true;
        }
    }

    private bool EvalDrops(FilterCondition c, MissionRowData m)
    {
        if (c.Op != "c" && c.Op != "dnc")
        {
            return true;
        }
        if (c.Val == "")
        {
            return true;
        }
        var parts = c.Val.Split('_');
        // composite is name_level_rarity_quality; quality (index 3) is ignored.
        bool Matches(ArtifactDropRowData d)
        {
            if (parts.Length > 0 && parts[0] != "%" && parts[0] != "" && d.ArtifactId.ToString(CultureInfo.InvariantCulture) != parts[0])
            {
                return false;
            }
            if (parts.Length > 1 && parts[1] != "%" && parts[1] != "" && d.Level.ToString(CultureInfo.InvariantCulture) != parts[1])
            {
                return false;
            }
            if (parts.Length > 2 && parts[2] != "%" && parts[2] != "" && d.Rarity.ToString(CultureInfo.InvariantCulture) != parts[2])
            {
                return false;
            }
            return true;
        }
        var exists = _dropsByMission[(m.PlayerId, m.MissionId)].Any(Matches);
        return c.Op == "dnc" ? !exists : exists;
    }

    private static bool CompareNumeric(FilterCondition c, long actual)
    {
        if (c.Op is not ("=" or "!=" or ">" or "<" or ">=" or "<="))
        {
            return true;
        }
        if (!IsInt(c.Val))
        {
            return true; // no-op condition, does not exclude
        }
        return CompareLong(c.Op, actual, long.Parse(c.Val, CultureInfo.InvariantCulture));
    }

    private bool EvalDate(FilterCondition c, long actualUnix)
    {
        long target;
        switch (c.Op)
        {
            case ">":
            case "<":
            case ">=":
            case "<=":
            case "=":
                if (!TimeBucket.TryParseDateToUnix(c.Val, out target))
                {
                    return true;
                }
                break;
            case "d=":
                if (!TimeBucket.TryParseDateToUnix(c.Val, out target))
                {
                    return true;
                }
                return actualUnix == target;
            default:
                return true;
        }
        return c.Op switch
        {
            ">" => actualUnix > target,
            "<" => actualUnix < target,
            ">=" => actualUnix >= target,
            "<=" => actualUnix <= target,
            "=" => actualUnix == target,
            _ => true,
        };
    }

    private static bool CompareLong(string op, long a, long b) => op switch
    {
        "=" => a == b,
        "!=" => a != b,
        ">" => a > b,
        "<" => a < b,
        ">=" => a >= b,
        "<=" => a <= b,
        _ => true,
    };

    private static bool CompareDouble(string op, double a, double b) => op switch
    {
        "=" => a == b,
        "!=" => a != b,
        ">" => a > b,
        "<" => a < b,
        ">=" => a >= b,
        "<=" => a <= b,
        _ => true,
    };

    private static bool CompareText(string op, string a, string b) => op switch
    {
        "=" => string.Equals(a, b, StringComparison.Ordinal),
        "!=" => !string.Equals(a, b, StringComparison.Ordinal),
        ">" => string.CompareOrdinal(a, b) > 0,
        "<" => string.CompareOrdinal(a, b) < 0,
        ">=" => string.CompareOrdinal(a, b) >= 0,
        "<=" => string.CompareOrdinal(a, b) <= 0,
        _ => true,
    };

    private static bool IsInt(string s) =>
        long.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _);

    private static bool IsFloat(string s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
}
