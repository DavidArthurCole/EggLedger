using EggLedger.Web.Missions;

namespace EggLedger.Web.Tests.Missions;

/// <summary>
/// Golden tests for <see cref="DropSorter"/> derived from
/// www/src/composables/useMissionSorting.ts: grouping/counting and the
/// comparator orderings (including the trailing reverse in
/// sortGroupAlreadyCombed).
/// </summary>
public sealed class DropSorterTests {
    private static DropLike D(int id, string name, int level, int rarity, double quality = 0, int iv = 0, string spec = "Artifact") =>
        new() { Id = id, Name = name, Level = level, Rarity = rarity, Quality = quality, IvOrder = iv, SpecType = spec };

    [Fact]
    public void GroupedSpecType_CombinesIdenticalAndCounts() {
        var input = new[]
        {
            D(1, "A", 0, 0),
            D(1, "A", 0, 0),
            D(2, "B", 1, 0),
        };
        var grouped = DropSorter.GroupedSpecType(input);
        Assert.Equal(2, grouped.Count);
        Assert.Equal(2, grouped[0].Count);
        Assert.Equal(1, grouped[1].Count);
    }

    [Fact]
    public void GroupedSpecType_KeyIncludesSpecType() {
        var input = new[]
        {
            D(1, "A", 0, 0, spec: "Artifact"),
            D(1, "A", 0, 0, spec: "Stone"),
        };
        var grouped = DropSorter.GroupedSpecType(input);
        Assert.Equal(2, grouped.Count);
    }

    [Fact]
    public void SortGroupAlreadyCombed_OrdersLevelAscRarityAsc() {
        // Comparator: level desc, rarity desc, id desc, quality asc; then reverse.
        // Net effect: level asc, rarity asc, id asc, quality desc.
        var input = new[]
        {
            D(1, "A", 2, 0),
            D(2, "B", 0, 3),
            D(3, "C", 0, 0),
        };
        var sorted = DropSorter.SortGroupAlreadyCombed(input);
        Assert.Equal(0, sorted[0].Level);
        Assert.Equal(0, sorted[0].Rarity); // id 3
        Assert.Equal(3, sorted[0].Id);
        Assert.Equal(0, sorted[1].Level);
        Assert.Equal(3, sorted[1].Rarity); // id 2
        Assert.Equal(2, sorted[2].Level); // id 1 last
    }

    [Fact]
    public void SortGroupAlreadyCombed_QualityDescendingWithinTie() {
        var input = new[]
        {
            D(1, "A", 0, 0, quality: 1),
            D(1, "A", 0, 0, quality: 5),
        };
        var sorted = DropSorter.SortGroupAlreadyCombed(input);
        Assert.Equal(5, sorted[0].Quality);
        Assert.Equal(1, sorted[1].Quality);
    }

    [Fact]
    public void InventoryVisualizerSort_RarityDescIvDescLevelDesc() {
        var input = new[]
        {
            D(1, "A", 0, 1, iv: 1),
            D(2, "B", 3, 3, iv: 5),
            D(3, "C", 0, 1, iv: 9),
        };
        var sorted = DropSorter.InventoryVisualizerSort(input);
        Assert.Equal(3, sorted[0].Rarity); // highest rarity first
        Assert.Equal(9, sorted[1].IvOrder); // among rarity 1, higher iv first
        Assert.Equal(1, sorted[2].IvOrder);
    }
}
