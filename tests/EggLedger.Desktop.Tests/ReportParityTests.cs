using EggLedger.Desktop.Storage;
using EggLedger.Domain.Reports;
using Microsoft.Data.Sqlite;

namespace EggLedger.Desktop.Tests;

/// <summary>
/// THE D2 PARITY GATE. Loads the same mission + artifact-drop fixtures into a real
/// SQLite mission DB and runs report definitions two ways:
///   1. SQL path: ReportExecutor over SqliteMissionDb (the live native path).
///   2. In-memory path: InMemoryReportRunner over typed rows (the browser path).
/// Both MUST produce an identical ReportResult (value equality via
/// ReportResult.Equals). Shapes cover 1D aggregate, mission-scope filter, 2D
/// pivot, drops EXISTS-subquery, drops-subject aggregate, time-bucketed series,
/// normalized (launches), and family-weighted aggregate, so the SQL WHERE clause,
/// the artifact_drops JOIN, the EXISTS subquery, strftime bucketing, and the
/// weighted cap_weight SUM are all exercised.
/// </summary>
public sealed class ReportParityTests {
    private const string Eid = "EI1";

    private sealed class NoWeights : IWeightData {
        public double CraftingWeight(long artifactId, long level) => 1;
        public IReadOnlyList<int> FamilyAfxIds(string familyId) => Array.Empty<int>();
    }

    private sealed class FixedFamily : IWeightData {
        private readonly int[] _ids;
        public FixedFamily(params int[] ids) => _ids = ids;
        public double CraftingWeight(long artifactId, long level) => 1;
        public IReadOnlyList<int> FamilyAfxIds(string familyId) => _ids;
    }

    // Typed-row fixture builders (mirrors InMemoryReportRunnerTests.M / D).
    private static MissionRowData M(
        string id, int ship, int duration, long start, long ret,
        int cap = 1, int nominal = 1, int target = 0, int type = 0,
        int level = 0, bool dub = false, bool bugged = false) => new() {
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
        int dropIndex = 0, double quality = 0, string spec = "Artifact") => new() {
            PlayerId = Eid,
            MissionId = mission,
            DropIndex = dropIndex,
            ArtifactId = artifactId,
            Rarity = rarity,
            Level = tier,
            Quality = quality,
            SpecType = spec,
        };

    // 1758067200 = the mission-type cutoff; keep all fixtures well after it and
    // within distinct months so the time-series buckets are stable.
    private static List<MissionRowData> Missions() =>
    [
        // Distinct per-group counts to avoid SQLite tie-order ambiguity in count-DESC
        // aggregates: ship 1 -> 3 missions, ship 2 -> 2, ship 3 -> 1.
        M("m1", ship: 1, duration: 0, start: 1758100000, ret: 1758100000 + 3600, cap: 10, nominal: 5, type: 0, level: 1),
        M("m2", ship: 1, duration: 0, start: 1758200000, ret: 1758200000 + 7200, cap: 10, nominal: 5, type: 0, level: 1),
        M("m3", ship: 1, duration: 1, start: 1761000000, ret: 1761000000 + 3600, cap: 8, nominal: 4, type: 1, level: 2),
        M("m4", ship: 2, duration: 0, start: 1758300000, ret: 1758300000 + 1800, cap: 6, nominal: 6, type: 0, level: 0),
        M("m5", ship: 2, duration: 1, start: 1763600000, ret: 1763600000 + 3600, cap: 6, nominal: 3, type: 0, level: 1),
        M("m6", ship: 3, duration: 2, start: 1766300000, ret: 1766300000 + 14400, cap: 4, nominal: 2, type: 1, level: 3),
    ];

    private static List<ArtifactDropRowData> Drops() =>
    [
        // m1: two drops (rarity 0, 1), artifact ids 12/13.
        D("m1", artifactId: 12, rarity: 0, tier: 1, dropIndex: 0, spec: "Artifact"),
        D("m1", artifactId: 13, rarity: 1, tier: 2, dropIndex: 1, spec: "Stone"),
        // m2: one drop rarity 1.
        D("m2", artifactId: 12, rarity: 1, tier: 1, dropIndex: 0, spec: "Artifact"),
        // m3: one drop rarity 3.
        D("m3", artifactId: 14, rarity: 3, tier: 2, dropIndex: 0, spec: "Artifact"),
        // m4: one drop rarity 0.
        D("m4", artifactId: 13, rarity: 0, tier: 1, dropIndex: 0, spec: "Stone"),
        // m5: zero-drop sentinel (drop_index -1, excluded by the engine).
        D("m5", artifactId: 0, rarity: 0, tier: 0, dropIndex: -1, spec: ""),
        // m6: two drops rarity 2.
        D("m6", artifactId: 12, rarity: 2, tier: 3, dropIndex: 0, spec: "Artifact"),
        D("m6", artifactId: 14, rarity: 2, tier: 1, dropIndex: 1, spec: "Artifact"),
    ];

    private static SqliteConnection SeedSqlite(IReadOnlyList<MissionRowData> missions, IReadOnlyList<ArtifactDropRowData> drops) {
        var name = "parity_" + Guid.NewGuid().ToString("N");
        var conn = new SqliteConnection($"Data Source={name};Mode=Memory;Cache=Shared");
        conn.Open();
        SqliteMigrationRunner.MigrateMissionDb(conn);

        using (var tx = conn.BeginTransaction()) {
            foreach (var m in missions) {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText =
                    @"INSERT INTO mission
                      (player_id, mission_id, start_timestamp, complete_payload, mission_type,
                       ship, duration_type, level, capacity, nominal_capacity, is_dub_cap, is_bugged_cap, target, return_timestamp)
                      VALUES (@pid, @mid, @start, @payload, @type, @ship, @dur, @lvl, @cap, @nom, @dub, @bug, @target, @ret);";
                cmd.Parameters.AddWithValue("@pid", m.PlayerId);
                cmd.Parameters.AddWithValue("@mid", m.MissionId);
                cmd.Parameters.AddWithValue("@start", m.StartTimestamp);
                cmd.Parameters.AddWithValue("@payload", Array.Empty<byte>());
                cmd.Parameters.AddWithValue("@type", m.MissionType);
                cmd.Parameters.AddWithValue("@ship", m.Ship);
                cmd.Parameters.AddWithValue("@dur", m.DurationType);
                cmd.Parameters.AddWithValue("@lvl", m.Level);
                cmd.Parameters.AddWithValue("@cap", m.Capacity);
                cmd.Parameters.AddWithValue("@nom", m.NominalCapacity);
                cmd.Parameters.AddWithValue("@dub", m.IsDubCap ? 1 : 0);
                cmd.Parameters.AddWithValue("@bug", m.IsBuggedCap ? 1 : 0);
                cmd.Parameters.AddWithValue("@target", m.Target);
                cmd.Parameters.AddWithValue("@ret", m.ReturnTimestamp);
                cmd.ExecuteNonQuery();
            }
            foreach (var d in drops) {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText =
                    @"INSERT INTO artifact_drops
                      (mission_id, player_id, drop_index, artifact_id, spec_type, level, rarity, quality)
                      VALUES (@mid, @pid, @idx, @aid, @spec, @lvl, @rar, @q);";
                cmd.Parameters.AddWithValue("@mid", d.MissionId);
                cmd.Parameters.AddWithValue("@pid", d.PlayerId);
                cmd.Parameters.AddWithValue("@idx", d.DropIndex);
                cmd.Parameters.AddWithValue("@aid", d.ArtifactId);
                cmd.Parameters.AddWithValue("@spec", d.SpecType);
                cmd.Parameters.AddWithValue("@lvl", d.Level);
                cmd.Parameters.AddWithValue("@rar", d.Rarity);
                cmd.Parameters.AddWithValue("@q", d.Quality);
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }
        return conn;
    }

    private static void AssertParity(ReportDefinition def, IWeightData weights) {
        var missions = Missions();
        var drops = Drops();

        using var conn = SeedSqlite(missions, drops);
        var sqlDb = new SqliteMissionDb(conn);
        var sqlResult = new ReportExecutor(sqlDb, weights).ExecuteReport(def);

        var memResult = new InMemoryReportRunner(weights).Run(def, missions, drops);

        Assert.Equal(memResult, sqlResult);
    }

    [Fact]
    public void Parity_Aggregate1D_ByShip() {
        AssertParity(
            new ReportDefinition { Mode = "aggregate", GroupBy = "ship_type", Subject = "missions", AccountId = Eid },
            new NoWeights());
    }

    [Fact]
    public void Parity_Aggregate1D_WithMissionFilter() {
        AssertParity(
            new ReportDefinition {
                Mode = "aggregate",
                GroupBy = "ship_type",
                Subject = "missions",
                AccountId = Eid,
                Filters = new ReportFilters {
                    And = [new FilterCondition { TopLevel = "duration", Op = "=", Val = "0" }],
                },
            },
            new NoWeights());
    }

    [Fact]
    public void Parity_Pivot2D_ShipByDuration() {
        AssertParity(
            new ReportDefinition {
                Mode = "aggregate",
                GroupBy = "ship_type",
                SecondaryGroupBy = "duration_type",
                Subject = "missions",
                AccountId = Eid,
            },
            new NoWeights());
    }

    [Fact]
    public void Parity_DropsSubject_ByRarity() {
        AssertParity(
            new ReportDefinition { Mode = "aggregate", GroupBy = "rarity", Subject = "artifacts", AccountId = Eid },
            new NoWeights());
    }

    [Fact]
    public void Parity_DropsExistsSubquery_Contains() {
        AssertParity(
            new ReportDefinition {
                Mode = "aggregate",
                GroupBy = "ship_type",
                Subject = "missions",
                AccountId = Eid,
                Filters = new ReportFilters {
                    And = [new FilterCondition { TopLevel = "drops", Op = "c", Val = "%_%_2_%" }],
                },
            },
            new NoWeights());
    }

    [Fact]
    public void Parity_DropsExistsSubquery_DoesNotContain() {
        AssertParity(
            new ReportDefinition {
                Mode = "aggregate",
                GroupBy = "ship_type",
                Subject = "missions",
                AccountId = Eid,
                Filters = new ReportFilters {
                    And = [new FilterCondition { TopLevel = "drops", Op = "dnc", Val = "%_%_0_%" }],
                },
            },
            new NoWeights());
    }

    [Fact]
    public void Parity_TimeSeries_ByMonth() {
        AssertParity(
            new ReportDefinition {
                Mode = "time_series",
                GroupBy = "time_bucket",
                TimeBucket = "month",
                Subject = "missions",
                AccountId = Eid,
            },
            new NoWeights());
    }

    [Fact]
    public void Parity_Normalized_Launches() {
        AssertParity(
            new ReportDefinition {
                Mode = "aggregate",
                GroupBy = "ship_type",
                Subject = "missions",
                AccountId = Eid,
                NormalizeBy = "launches",
            },
            new NoWeights());
    }

    [Fact]
    public void Parity_Normalized_Airtime() {
        AssertParity(
            new ReportDefinition {
                Mode = "aggregate",
                GroupBy = "ship_type",
                Subject = "missions",
                AccountId = Eid,
                NormalizeBy = "airtime",
            },
            new NoWeights());
    }

    [Fact]
    public void Parity_FamilyWeighted_Aggregate() {
        AssertParity(
            new ReportDefinition {
                Subject = "artifacts",
                Mode = "aggregate",
                GroupBy = "ship_type",
                FamilyWeight = "tachyon-stone",
                AccountId = Eid,
            },
            new FixedFamily(12, 13));
    }

    [Fact]
    public void Parity_TimePivot_MonthByShip() {
        AssertParity(
            new ReportDefinition {
                Mode = "time_series",
                GroupBy = "time_bucket",
                SecondaryGroupBy = "ship_type",
                TimeBucket = "month",
                Subject = "missions",
                AccountId = Eid,
            },
            new NoWeights());
    }
}
