using EggLedger.Domain.MissionQuery;
using EggLedger.Web.Missions;

namespace EggLedger.Web.Tests.Missions;

/// <summary>
/// Golden parity with the mergeItems/bucket logic in www/src/views/LifetimeDataView.vue.
/// Guards the empty-artifacts history: real input must produce non-empty buckets.
/// </summary>
public sealed class LifetimeAggregatorTests {
    private static MissionDrop Drop(
        int id,
        string spec,
        int level = 0,
        int rarity = 0,
        string name = "X",
        double quality = 0,
        int iv = 0) =>
        new() {
            Id = id,
            SpecType = spec,
            Name = name,
            GameName = name,
            Level = level,
            Rarity = rarity,
            Quality = quality,
            IVOrder = iv,
        };

    private static Dictionary<string, List<MissionDrop>> Missions(params (string Id, MissionDrop[] Drops)[] missions) {
        var d = new Dictionary<string, List<MissionDrop>>();
        foreach (var (id, drops) in missions) {
            d[id] = [.. drops];
        }
        return d;
    }

    [Fact]
    public void Aggregate_RepresentativeInput_ProducesNonEmptyGroups() {
        // Guards the empty-artifacts regression: a real spread of spec types must produce one non-empty list per bucket.
        var input = Missions(
            ("m1", new[]
            {
                Drop(1, "Artifact", level: 2, rarity: 3, name: "TACHYON_DEFLECTOR"),
                Drop(2, "Stone", name: "TACHYON_STONE"),
                Drop(3, "StoneFragment", name: "TACHYON_STONE_FRAGMENT"),
                Drop(4, "Ingredient", name: "GOLD_METEORITE"),
            }),
            ("m2", new[]
            {
                Drop(1, "Artifact", level: 2, rarity: 3, name: "TACHYON_DEFLECTOR"),
            }));

        var result = LifetimeAggregator.Aggregate(input);

        Assert.NotEmpty(result.Artifacts);
        Assert.NotEmpty(result.Stones);
        Assert.NotEmpty(result.StoneFragments);
        Assert.NotEmpty(result.Ingredients);
        Assert.Equal(2, result.MissionCount);
    }

    [Fact]
    public void Aggregate_CombinesIdenticalDropsAndSumsCount() {
        // Same id+level+rarity across two missions merges to one representative, count 2.
        var input = Missions(
            ("m1", new[] { Drop(1, "Artifact", level: 5, rarity: 2) }),
            ("m2", new[] { Drop(1, "Artifact", level: 5, rarity: 2) }));

        var result = LifetimeAggregator.Aggregate(input);

        Assert.Single(result.Artifacts);
        Assert.Equal(2, result.Artifacts[0].Count);
    }

    [Fact]
    public void Aggregate_MergeKeyIsIdLevelRarity_NotName() {
        // The Vue merge key is id_level_rarity, not name or specType, so same id different level splits.
        var input = Missions(
            ("m1", new[]
            {
                Drop(1, "Artifact", level: 1, rarity: 0),
                Drop(1, "Artifact", level: 2, rarity: 0),
            }));

        var result = LifetimeAggregator.Aggregate(input);

        Assert.Equal(2, result.Artifacts.Count);
        Assert.Equal(1, result.Artifacts[0].Count);
        Assert.Equal(1, result.Artifacts[1].Count);
    }

    [Fact]
    public void Aggregate_DifferentRarity_AreSeparateGroups() {
        var input = Missions(
            ("m1", new[]
            {
                Drop(1, "Artifact", level: 0, rarity: 0),
                Drop(1, "Artifact", level: 0, rarity: 1),
            }));

        var result = LifetimeAggregator.Aggregate(input);

        Assert.Equal(2, result.Artifacts.Count);
    }

    [Fact]
    public void Aggregate_RoutesEachSpecTypeToItsBucket() {
        var input = Missions(
            ("m1", new[]
            {
                Drop(1, "Artifact"),
                Drop(2, "Stone"),
                Drop(3, "StoneFragment"),
                Drop(4, "Ingredient"),
            }));

        var result = LifetimeAggregator.Aggregate(input);

        Assert.Single(result.Artifacts);
        Assert.Single(result.Stones);
        Assert.Single(result.StoneFragments);
        Assert.Single(result.Ingredients);
    }

    [Fact]
    public void Aggregate_UnknownSpecType_IsDropped() {
        // A spec type the Vue split does not match has no bucket.
        var input = Missions(("m1", new[] { Drop(1, "Mystery") }));

        var result = LifetimeAggregator.Aggregate(input);

        Assert.Empty(result.Artifacts);
        Assert.Empty(result.Stones);
        Assert.Empty(result.StoneFragments);
        Assert.Empty(result.Ingredients);
    }

    [Fact]
    public void Aggregate_FirstOccurrenceIsRepresentative_PreservesOrder() {
        var input = Missions(
            ("m1", new[]
            {
                Drop(5, "Artifact", level: 0, name: "FIRST"),
                Drop(6, "Artifact", level: 0, name: "SECOND"),
            }));

        var result = LifetimeAggregator.Aggregate(input);

        Assert.Equal("FIRST", result.Artifacts[0].Name);
        Assert.Equal("SECOND", result.Artifacts[1].Name);
    }

    [Fact]
    public void Aggregate_PreservesDisplayFields() {
        var input = Missions(
            ("m1", new[] { Drop(9, "Artifact", level: 3, rarity: 2, name: "QUANTUM", quality: 4.5, iv: 7) }));

        var result = LifetimeAggregator.Aggregate(input);

        var d = result.Artifacts[0];
        Assert.Equal(9, d.Id);
        Assert.Equal("QUANTUM", d.Name);
        Assert.Equal(3, d.Level);
        Assert.Equal(2, d.Rarity);
        Assert.Equal(4.5, d.Quality);
        Assert.Equal(7, d.IvOrder);
        Assert.Equal("Artifact", d.SpecType);
    }

    [Fact]
    public void Aggregate_EmptyInput_GivesEmptyGroupsAndZeroCount() {
        var result = LifetimeAggregator.Aggregate(new Dictionary<string, List<MissionDrop>>());

        Assert.Empty(result.Artifacts);
        Assert.Equal(0, result.MissionCount);
    }
}
