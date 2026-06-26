using EggLedger.Domain.Reports;
using EggLedger.Web.Data;

namespace EggLedger.Web.Tests.Data;

public sealed class IndexedDbMissionDbTests {
    private const string Eid = "EI1";

    private sealed class NoWeights : IWeightData {
        public double CraftingWeight(long artifactId, long level) => 1;
        public IReadOnlyList<int> FamilyAfxIds(string familyId) => Array.Empty<int>();
    }

    private static MissionRow Mission(string id, int ship, double start) => new() {
        PlayerId = Eid,
        MissionId = id,
        Ship = ship,
        StartTimestamp = start,
        ReturnTimestamp = start + 100,
        Capacity = 1,
        NominalCapacity = 1,
    };

    [Fact]
    public async Task RunReportAsync_AggregateByShip_RunsEndToEnd() {
        var db = new FakeIndexedDb();
        db.Seed("mission", Mission("a", ship: 9, start: 100));
        db.Seed("mission", Mission("b", ship: 9, start: 200));
        db.Seed("mission", Mission("c", ship: 3, start: 300));
        db.Seed("mission", Mission("other", ship: 9, start: 400) with { PlayerId = "EI2" });

        var sut = new IndexedDbMissionDb(db, new NoWeights());
        var def = new ReportDefinition {
            Mode = "aggregate",
            GroupBy = "ship_type",
            Subject = "missions",
            AccountId = Eid,
        };

        var result = await sut.RunReportAsync(def, Eid);

        Assert.False(result.Is2D);
        Assert.Equal([2, 1], result.Values);
        Assert.Equal("Henerprise", result.Labels[0]);
        Assert.Equal("BCR", result.Labels[1]);
    }

    [Fact]
    public async Task RunReportAsync_DropBasedByRarity_ReadsDropStore() {
        var db = new FakeIndexedDb();
        db.Seed("mission", Mission("a", ship: 9, start: 100));
        db.Seed("artifact_drops", new ArtifactDropRow {
            PlayerId = Eid,
            MissionId = "a",
            DropIndex = 0,
            ArtifactId = 12,
            Rarity = 3,
            Level = 2,
        });
        db.Seed("artifact_drops", new ArtifactDropRow {
            PlayerId = Eid,
            MissionId = "a",
            DropIndex = 1,
            ArtifactId = 13,
            Rarity = 3,
            Level = 1,
        });
        db.Seed("artifact_drops", new ArtifactDropRow {
            PlayerId = Eid,
            MissionId = "a",
            DropIndex = 2,
            ArtifactId = 14,
            Rarity = 1,
            Level = 0,
        });
        // Other player's drop must be ignored.
        db.Seed("artifact_drops", new ArtifactDropRow {
            PlayerId = "EI2",
            MissionId = "z",
            DropIndex = 0,
            ArtifactId = 99,
            Rarity = 3,
            Level = 0,
        });

        var sut = new IndexedDbMissionDb(db, new NoWeights());
        var def = new ReportDefinition {
            Mode = "aggregate",
            GroupBy = "rarity",
            Subject = "artifacts",
            AccountId = Eid,
        };

        var result = await sut.RunReportAsync(def, Eid);

        // rarity 3 -> 2 drops, rarity 1 -> 1 drop; descending by count.
        Assert.Equal([2, 1], result.Values);
        Assert.Equal("Legendary", result.Labels[0]);
        Assert.Equal("Rare", result.Labels[1]);
    }

    [Fact]
    public async Task RunReportAsync_EmptyWhenNoRows() {
        var db = new FakeIndexedDb();
        var sut = new IndexedDbMissionDb(db, new NoWeights());
        var def = new ReportDefinition {
            Mode = "aggregate",
            GroupBy = "ship_type",
            Subject = "missions",
            AccountId = Eid,
        };

        var result = await sut.RunReportAsync(def, Eid);

        Assert.Empty(result.Values);
        Assert.Empty(result.Labels);
    }
}
