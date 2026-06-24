using System.Globalization;
using EggLedger.Domain.Reports;

namespace EggLedger.Domain.Tests.Reports;

// Parity oracle for the SQL-free browser path. For each report definition we run
// BOTH execution paths over ONE source of truth (typed rows):
//   - SQL path: the existing ReportExecutor over a FakeDb seeded with the grouped
//     rows SQLite would return, derived from the typed rows by a small explicit
//     oracle here (independent of InMemoryMissionDb's internal grouping).
//   - In-memory path: InMemoryReportRunner over the same typed rows.
// The two ReportResults must be equal.
public class InMemoryReportRunnerTests
{
    private const string Eid = "EI1";

    private sealed class NoWeights : IWeightData
    {
        public double CraftingWeight(long artifactId, long level) => 1;
        public IReadOnlyList<int> FamilyAfxIds(string familyId) => Array.Empty<int>();
    }

    private sealed class FixedFamily : IWeightData
    {
        private readonly int[] _ids;
        public FixedFamily(params int[] ids) => _ids = ids;
        public double CraftingWeight(long artifactId, long level) => 1;
        public IReadOnlyList<int> FamilyAfxIds(string familyId) => _ids;
    }

    // FakeDb that matches a query by a unique substring and serves canned rows,
    // mirroring the A7 ExecuteTests double.
    private sealed class FakeDb : IMissionDb
    {
        private readonly List<(string Key, IReadOnlyList<object?[]> Rows)> _byKey = [];

        public FakeDb On(string contains, IReadOnlyList<object?[]> rows)
        {
            _byKey.Add((contains, rows));
            return this;
        }

        public IReadOnlyList<object?[]> Query(string sql, IReadOnlyList<object?> args)
        {
            foreach (var (key, rows) in _byKey)
            {
                if (sql.Contains(key, StringComparison.Ordinal))
                {
                    return rows;
                }
            }
            return Array.Empty<object?[]>();
        }
    }

    private static MissionRowData M(
        string id, int ship, int duration, long start, long ret,
        int cap = 1, int nominal = 1, int target = 0, int type = 0,
        int level = 0, bool dub = false, bool bugged = false) => new()
        {
            PlayerId = Eid,
            MissionId = id,
            Ship = ship,
            DurationType = duration,
            Level = level,
            Target = target,
            MissionType = type,
            StartTimestamp = start,
            ReturnTimestamp = ret,
            Capacity = cap,
            NominalCapacity = nominal,
            IsDubCap = dub,
            IsBuggedCap = bugged,
        };

    private static ArtifactDropRowData D(
        string mission, int artifactId, int rarity, int tier,
        int dropIndex = 0, double quality = 0, string spec = "Artifact") => new()
        {
            PlayerId = Eid,
            MissionId = mission,
            DropIndex = dropIndex,
            ArtifactId = artifactId,
            Rarity = rarity,
            Level = tier,
            Quality = quality,
            SpecType = spec,
        };

    // 1D count over a mission column, descending by count, first-seen tiebreak.
    private static object?[][] Group1D(IEnumerable<MissionRowData> rows, Func<MissionRowData, string> key)
    {
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var r in rows)
        {
            var k = key(r);
            if (!counts.ContainsKey(k))
            {
                order.Add(k);
            }
            counts.TryGetValue(k, out var cur);
            counts[k] = cur + 1;
        }
        return [.. order
            .Select((k, i) => (k, i, c: counts[k]))
            .OrderByDescending(x => x.c)
            .ThenBy(x => x.i)
            .Select(x => new object?[] { x.k, x.c })];
    }

    // 2D count over two mission columns, ascending by raw1 then raw2 (numeric).
    private static object?[][] Group2D(
        IEnumerable<MissionRowData> rows, Func<MissionRowData, string> k1, Func<MissionRowData, string> k2)
    {
        var counts = new Dictionary<(string, string), long>();
        foreach (var r in rows)
        {
            var key = (k1(r), k2(r));
            counts.TryGetValue(key, out var cur);
            counts[key] = cur + 1;
        }
        return [.. counts
            .OrderBy(kv => long.Parse(kv.Key.Item1, CultureInfo.InvariantCulture))
            .ThenBy(kv => long.Parse(kv.Key.Item2, CultureInfo.InvariantCulture))
            .Select(kv => new object?[] { kv.Key.Item1, kv.Key.Item2, kv.Value })];
    }

    private static string Ship(MissionRowData m) => m.Ship.ToString(CultureInfo.InvariantCulture);
    private static string Dur(MissionRowData m) => m.DurationType.ToString(CultureInfo.InvariantCulture);

    [Fact]
    public void Parity_AggregateByShip()
    {
        var missions = new List<MissionRowData>
        {
            M("a", ship: 9, duration: 0, start: 1_700_000_000, ret: 1_700_003_600),
            M("b", ship: 9, duration: 1, start: 1_700_100_000, ret: 1_700_103_600),
            M("c", ship: 3, duration: 0, start: 1_700_200_000, ret: 1_700_203_600),
        };
        var def = new ReportDefinition { Mode = "aggregate", GroupBy = "ship_type", Subject = "missions", AccountId = Eid };

        var sqlDb = new FakeDb().On("GROUP BY m.ship", Group1D(missions, Ship));
        var sqlResult = new ReportExecutor(sqlDb, new NoWeights()).ExecuteReport(def);
        var memResult = new InMemoryReportRunner(new NoWeights()).Run(def, missions, Array.Empty<ArtifactDropRowData>());

        Assert.Equal(sqlResult, memResult);
        Assert.Equal([2, 1], memResult.Values);
        Assert.Equal("Henerprise", memResult.Labels[0]);
    }

    [Fact]
    public void Parity_AggregateByShip_WithFilter()
    {
        // Mission-scope filter: duration_type = 0 keeps a and c.
        var missions = new List<MissionRowData>
        {
            M("a", ship: 9, duration: 0, start: 1_700_000_000, ret: 1_700_003_600),
            M("b", ship: 9, duration: 1, start: 1_700_100_000, ret: 1_700_103_600),
            M("c", ship: 3, duration: 0, start: 1_700_200_000, ret: 1_700_203_600),
        };
        var def = new ReportDefinition
        {
            Mode = "aggregate",
            GroupBy = "ship_type",
            Subject = "missions",
            AccountId = Eid,
            Filters = new ReportFilters
            {
                And = [new FilterCondition { TopLevel = "duration", Op = "=", Val = "0" }],
            },
        };

        var kept = missions.Where(m => m.DurationType == 0);
        var sqlDb = new FakeDb().On("GROUP BY m.ship", Group1D(kept, Ship));
        var sqlResult = new ReportExecutor(sqlDb, new NoWeights()).ExecuteReport(def);
        var memResult = new InMemoryReportRunner(new NoWeights()).Run(def, missions, Array.Empty<ArtifactDropRowData>());

        Assert.Equal(sqlResult, memResult);
        // Both ships have one duration-0 mission; equal counts keep first-seen order.
        Assert.Equal([1, 1], memResult.Values);
    }

    [Fact]
    public void Parity_PivotShipByDuration()
    {
        var missions = new List<MissionRowData>
        {
            M("a", ship: 3, duration: 1, start: 1, ret: 2),
            M("b", ship: 9, duration: 0, start: 1, ret: 2),
            M("c", ship: 9, duration: 1, start: 1, ret: 2),
            M("d", ship: 9, duration: 1, start: 1, ret: 2),
        };
        var def = new ReportDefinition
        {
            Mode = "aggregate",
            GroupBy = "ship_type",
            SecondaryGroupBy = "duration_type",
            Subject = "missions",
            AccountId = Eid,
        };

        var sqlDb = new FakeDb().On("GROUP BY m.ship, m.duration_type", Group2D(missions, Ship, Dur));
        var sqlResult = new ReportExecutor(sqlDb, new NoWeights()).ExecuteReport(def);
        var memResult = new InMemoryReportRunner(new NoWeights()).Run(def, missions, Array.Empty<ArtifactDropRowData>());

        Assert.Equal(sqlResult, memResult);
        Assert.True(memResult.Is2D);
        Assert.Equal(["BCR", "Henerprise"], memResult.RowLabels);
        Assert.Equal(["Short", "Standard"], memResult.ColLabels);
        // BCR/Short=0, BCR/Standard=1, Hen/Short=1, Hen/Standard=2.
        Assert.Equal([0, 1, 1, 2], memResult.MatrixValues);
    }

    [Fact]
    public void Parity_DropBasedAggregateByRarity()
    {
        // artifacts subject: count drops grouped by rarity (drop_index >= 0).
        var missions = new List<MissionRowData>
        {
            M("a", ship: 9, duration: 0, start: 1, ret: 2),
            M("b", ship: 3, duration: 0, start: 1, ret: 2),
        };
        var drops = new List<ArtifactDropRowData>
        {
            D("a", artifactId: 12, rarity: 3, tier: 2),
            D("a", artifactId: 13, rarity: 3, tier: 1, dropIndex: 1),
            D("b", artifactId: 14, rarity: 1, tier: 0),
            D("b", artifactId: 15, rarity: 1, tier: 0, dropIndex: 2),
            D("b", artifactId: 16, rarity: 0, tier: 0, dropIndex: -1), // excluded
        };
        var def = new ReportDefinition { Mode = "aggregate", GroupBy = "rarity", Subject = "artifacts", AccountId = Eid };

        // Oracle: count drops with drop_index >= 0 by rarity, desc by count.
        var keptDrops = drops.Where(d => d.DropIndex >= 0);
        var rarityRows = Group1DRaw(keptDrops.Select(d => d.Rarity.ToString(CultureInfo.InvariantCulture)));
        var sqlDb = new FakeDb().On("GROUP BY d.rarity", rarityRows);
        var sqlResult = new ReportExecutor(sqlDb, new NoWeights()).ExecuteReport(def);
        var memResult = new InMemoryReportRunner(new NoWeights()).Run(def, missions, drops);

        Assert.Equal(sqlResult, memResult);
        // rarity 1 -> 2 drops, rarity 3 -> 2 drops; equal, first-seen order is 3 then 1.
        Assert.Equal([2, 2], memResult.Values);
    }

    [Fact]
    public void Parity_DropFilterContains()
    {
        // Mission aggregate with a drops "contains" filter (rarity 3 exists).
        var missions = new List<MissionRowData>
        {
            M("a", ship: 9, duration: 0, start: 1, ret: 2),
            M("b", ship: 3, duration: 0, start: 1, ret: 2),
        };
        var drops = new List<ArtifactDropRowData>
        {
            D("a", artifactId: 12, rarity: 3, tier: 2),
            D("b", artifactId: 14, rarity: 1, tier: 0),
        };
        var def = new ReportDefinition
        {
            Mode = "aggregate",
            GroupBy = "ship_type",
            Subject = "missions",
            AccountId = Eid,
            Filters = new ReportFilters
            {
                And = [new FilterCondition { TopLevel = "drops", Op = "c", Val = "%_%_3_%" }],
            },
        };

        // Oracle: only mission a has a rarity-3 drop.
        var sqlDb = new FakeDb().On("GROUP BY m.ship", Group1D(missions.Where(m => m.MissionId == "a"), Ship));
        var sqlResult = new ReportExecutor(sqlDb, new NoWeights()).ExecuteReport(def);
        var memResult = new InMemoryReportRunner(new NoWeights()).Run(def, missions, drops);

        Assert.Equal(sqlResult, memResult);
        Assert.Single(memResult.Values);
        Assert.Equal("Henerprise", memResult.Labels[0]);
    }

    [Fact]
    public void Parity_TimeSeriesByMonth()
    {
        // Two months, one with two missions. UTC epochs:
        // 1700000000 -> 2023-11, 1704067200 -> 2024-01.
        var missions = new List<MissionRowData>
        {
            M("a", ship: 9, duration: 0, start: 1_700_000_000, ret: 1_700_003_600),
            M("b", ship: 9, duration: 0, start: 1_700_500_000, ret: 1_700_503_600),
            M("c", ship: 3, duration: 0, start: 1_704_067_200, ret: 1_704_070_800),
        };
        var def = new ReportDefinition
        {
            Mode = "time_series",
            GroupBy = "time_bucket",
            TimeBucket = "month",
            Subject = "missions",
            AccountId = Eid,
        };

        var bucketRows = Group1DRaw(missions
            .Select(m => TimeBucketLabel(m.StartTimestamp))
            .OrderBy(b => b, StringComparer.Ordinal));
        // Time series query is ordered ASC by bucket.
        var ordered = bucketRows.OrderBy(r => (string)r[0]!, StringComparer.Ordinal).ToArray();
        var sqlDb = new FakeDb().On("GROUP BY bucket", ordered);
        var sqlResult = new ReportExecutor(sqlDb, new NoWeights()).ExecuteReport(def);
        var memResult = new InMemoryReportRunner(new NoWeights()).Run(def, missions, Array.Empty<ArtifactDropRowData>());

        Assert.Equal(sqlResult, memResult);
        // 2023-11 (2 missions), then gap-filled 2023-12 (0), then 2024-01 (1).
        Assert.Equal(["2023-11", "2023-12", "2024-01"], memResult.Labels);
        Assert.Equal([2, 0, 1], memResult.Values);
    }

    [Fact]
    public void Parity_NormalizedAggregate_Launches()
    {
        // launches normalization divides each group's count by the per-group
        // mission count -> all ones here (denominator == numerator).
        var missions = new List<MissionRowData>
        {
            M("a", ship: 9, duration: 0, start: 1, ret: 2),
            M("b", ship: 9, duration: 1, start: 1, ret: 2),
            M("c", ship: 3, duration: 0, start: 1, ret: 2),
        };
        var def = new ReportDefinition
        {
            Mode = "aggregate",
            GroupBy = "ship_type",
            Subject = "missions",
            AccountId = Eid,
            NormalizeBy = "launches",
        };

        var grouped = Group1D(missions, Ship);
        var sqlDb = new FakeDb()
            .On("GROUP BY m.ship\n", grouped)        // main aggregate (has ORDER BY)
            .On("GROUP BY m.ship", grouped);          // denom1D count (no ORDER BY)
        var sqlResult = new ReportExecutor(sqlDb, new NoWeights()).ExecuteReport(def);
        var memResult = new InMemoryReportRunner(new NoWeights()).Run(def, missions, Array.Empty<ArtifactDropRowData>());

        Assert.Equal(sqlResult, memResult);
        Assert.True(memResult.IsFloat);
        Assert.All(memResult.FloatValues, v => Assert.Equal(1.0, v));
    }

    [Fact]
    public void Parity_FamilyWeightedAggregate()
    {
        // Family weight restricts to artifact ids {12, 13}; cap_weight = nominal/cap.
        var missions = new List<MissionRowData>
        {
            M("a", ship: 9, duration: 0, start: 1, ret: 2, cap: 4, nominal: 8),   // cap_weight 2.0
            M("b", ship: 3, duration: 0, start: 1, ret: 2, cap: 4, nominal: 4),   // cap_weight 1.0
        };
        var drops = new List<ArtifactDropRowData>
        {
            D("a", artifactId: 12, rarity: 3, tier: 0),
            D("b", artifactId: 13, rarity: 1, tier: 0),
            D("b", artifactId: 99, rarity: 0, tier: 0), // outside family, excluded
        };
        var weights = new FixedFamily(12, 13);
        var def = new ReportDefinition
        {
            Subject = "artifacts",
            Mode = "aggregate",
            GroupBy = "ship_type",
            FamilyWeight = "tachyon-stone",
            AccountId = Eid,
        };

        // Oracle rows: rawLabel, artifactId, level, cap_weight (grouped by ship/afx/level).
        var sqlDb = new FakeDb().On("cap_weight", new object?[][]
        {
            ["9", 12L, 0L, 2.0],
            ["3", 13L, 0L, 1.0],
        });
        var sqlResult = new ReportExecutor(sqlDb, weights).ExecuteReport(def);
        var memResult = new InMemoryReportRunner(weights).Run(def, missions, drops);

        Assert.Equal(sqlResult, memResult);
        Assert.True(memResult.IsFloat);
        Assert.Equal([2.0, 1.0], memResult.FloatValues);
    }

    [Fact]
    public void Parity_FamilyWeightedPivot_MissionCountCountsMissionsNotDrops()
    {
        // Family-weighted PIVOT over mission-scope dimensions (ship x duration).
        // Mission "a" carries TWO in-family drops. The mission-count matrix query is
        // FROM mission m with NO drops join, so it must count mission "a" ONCE, not
        // twice. The old code routed Count2D through the per-drop join (Subject is
        // "artifacts" for any family-weighted report), inflating the count.
        var missions = new List<MissionRowData>
        {
            M("a", ship: 9, duration: 0, start: 1, ret: 3, cap: 4, nominal: 8),   // cap_weight 2.0
            M("b", ship: 3, duration: 1, start: 1, ret: 3, cap: 4, nominal: 4),   // cap_weight 1.0
        };
        var drops = new List<ArtifactDropRowData>
        {
            D("a", artifactId: 12, rarity: 3, tier: 0, dropIndex: 0),
            D("a", artifactId: 13, rarity: 1, tier: 0, dropIndex: 1),  // second in-family drop on mission a
            D("b", artifactId: 12, rarity: 0, tier: 0, dropIndex: 0),
        };
        var weights = new FixedFamily(12, 13);
        var def = new ReportDefinition
        {
            Subject = "artifacts",
            Mode = "aggregate",
            GroupBy = "ship_type",
            SecondaryGroupBy = "duration_type",
            FamilyWeight = "tachyon-stone",
            AccountId = Eid,
        };

        // SQL oracle, hand-rolled independently of InMemoryMissionDb grouping:
        // cap_weight value query (artifact join), grouped by ship, duration, afx, level.
        // Mission a: ship 9, dur 0, two drops afx 12/13 -> two rows each cap_weight 2.0.
        // Mission b: ship 3, dur 1, one drop afx 12 -> one row cap_weight 1.0.
        var capRows = new object?[][]
        {
            ["9", "0", 12L, 0L, 2.0],
            ["9", "0", 13L, 0L, 2.0],
            ["3", "1", 12L, 0L, 1.0],
        };
        // mission-count query (FROM mission m, no join): one row per mission.
        // ship 9/dur 0 -> 1 mission, ship 3/dur 1 -> 1 mission.
        var missionCountRows = new object?[][]
        {
            ["3", "1", 1L],
            ["9", "0", 1L],
        };
        var sqlDb = new FakeDb()
            .On("cap_weight", capRows)
            .On("FROM mission m", missionCountRows);
        var sqlResult = new ReportExecutor(sqlDb, weights).ExecuteReport(def);
        var memResult = new InMemoryReportRunner(weights).Run(def, missions, drops);

        Assert.Equal(sqlResult, memResult);
        Assert.True(memResult.Is2D);
        // Mission count matrix must total 2 (two missions), not 3 (three drops).
        Assert.NotNull(memResult.MissionCountMatrix);
        Assert.Equal(2L, memResult.MissionCountMatrix!.Sum());
    }

    // 1D count over arbitrary raw key strings, desc by count, first-seen tiebreak.
    private static object?[][] Group1DRaw(IEnumerable<string> keys)
    {
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var k in keys)
        {
            if (!counts.ContainsKey(k))
            {
                order.Add(k);
            }
            counts.TryGetValue(k, out var cur);
            counts[k] = cur + 1;
        }
        return [.. order
            .Select((k, i) => (k, i, c: counts[k]))
            .OrderByDescending(x => x.c)
            .ThenBy(x => x.i)
            .Select(x => new object?[] { x.k, x.c })];
    }

    private static string TimeBucketLabel(long unix)
    {
        var t = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unix);
        return t.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    }
}
