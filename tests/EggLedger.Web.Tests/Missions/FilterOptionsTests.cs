using EggLedger.Domain.MissionQuery;
using EggLedger.Web.Missions;

namespace EggLedger.Web.Tests.Missions;

/// <summary>
/// Golden tests for <see cref="FilterOptions"/> derived from
/// www/src/utils/filterOptions.ts: option counts, value encodings, dedup/sort,
/// and the drop-option wildcard rows.
/// </summary>
public sealed class FilterOptionsTests {
    [Fact]
    public void ShipOptions_ElevenShipsValuedByIndex() {
        var opts = FilterOptions.GetShipFilterOptions();
        Assert.Equal(11, opts.Count);
        Assert.Equal("Chicken One", opts[0].Text);
        Assert.Equal("0", opts[0].Value);
        Assert.Equal("Atreggies Henliner", opts[10].Text);
        Assert.Equal("10", opts[10].Value);
    }

    [Fact]
    public void DurationOptions_HaveDurationStyleClass() {
        var opts = FilterOptions.GetDurationFilterOptions();
        Assert.Equal(4, opts.Count);
        Assert.Equal("text-duration-0", opts[0].StyleClass);
        Assert.Equal("Tutorial", opts[3].Text);
    }

    [Fact]
    public void LevelOptions_NineStarredLevels() {
        var opts = FilterOptions.GetLevelFilterOptions();
        Assert.Equal(9, opts.Count);
        Assert.Equal("0★", opts[0].Text);
        Assert.Equal("8★", opts[8].Text);
        Assert.Equal("8", opts[8].Value);
    }

    [Fact]
    public void MissionTypeOptions_HomeVirtueUnknown() {
        var opts = FilterOptions.GetMissionTypeFilterOptions();
        Assert.Collection(opts,
            o => Assert.Equal(("Home", "0"), (o.Text, o.Value)),
            o => Assert.Equal(("Virtue", "1"), (o.Text, o.Value)),
            o => Assert.Equal(("Unknown", "-1"), (o.Text, o.Value)));
    }

    [Fact]
    public void MissionFilterValueOptions_DispatchesByKey() {
        Assert.Equal(11, FilterOptions.GetMissionFilterValueOptions("ship").Count);
        Assert.Equal(2, FilterOptions.GetMissionFilterValueOptions("farm").Count);
        Assert.Equal(2, FilterOptions.GetMissionFilterValueOptions("dubcap").Count);
        Assert.Empty(FilterOptions.GetMissionFilterValueOptions("target"));
        Assert.Empty(FilterOptions.GetMissionFilterValueOptions("drops"));
    }

    [Fact]
    public void TargetOptions_MapIdAndImage() {
        var targets = new[]
        {
            new PossibleTarget { DisplayName = "None (Pre 1.27)", Id = -1, ImageString = "none.png" },
            new PossibleTarget { DisplayName = "Tachyon", Id = 1, ImageString = "tach.png" },
        };
        var opts = FilterOptions.GetTargetFilterOptions(targets);
        Assert.Equal("-1", opts[0].Value);
        Assert.Equal("none.png", opts[0].ImagePath);
        Assert.Equal("1", opts[1].Value);
    }

    [Fact]
    public void ArtifactTierOptions_DedupedSortedAscending() {
        var arts = new[]
        {
            new PossibleArtifact { Name = 1, Level = 2 },
            new PossibleArtifact { Name = 2, Level = 0 },
            new PossibleArtifact { Name = 3, Level = 2 },
        };
        var opts = FilterOptions.GetArtifactTierFilterOptions(arts);
        Assert.Equal(2, opts.Count);
        Assert.Equal("Tier 1", opts[0].Text);
        Assert.Equal("0", opts[0].Value);
        Assert.Equal("Tier 3", opts[1].Text);
        Assert.Equal("2", opts[1].Value);
    }

    [Fact]
    public void ArtifactNameOptions_RepresentativeIsLowestLevelSortedByText() {
        var arts = new[]
        {
            new PossibleArtifact { Name = 10, ProtoName = "ZETA", DisplayName = "Zeta", Level = 1 },
            new PossibleArtifact { Name = 10, ProtoName = "ZETA", DisplayName = "Zeta Low", Level = 0 },
            new PossibleArtifact { Name = 5, ProtoName = "ALPHA", DisplayName = "Alpha", Level = 0 },
        };
        var opts = FilterOptions.GetArtifactNameFilterOptions(arts);
        Assert.Equal(2, opts.Count);
        // Alphabetical by representative display text.
        Assert.Equal("Alpha", opts[0].Text);
        Assert.Equal("5", opts[0].Value);
        // Zeta family: lowest level (0) representative wins.
        Assert.Equal("Zeta Low", opts[1].Text);
        Assert.Equal("10", opts[1].Value);
    }

    [Fact]
    public void DropOptions_LeadWithAnyRarityTrio() {
        var opts = FilterOptions.GetDropFilterOptions(Array.Empty<PossibleArtifact>(), 100, advanced: false);
        Assert.Equal(3, opts.Count);
        Assert.Equal("%_%_1_%", opts[0].Value);
        Assert.Equal("%_%_2_%", opts[1].Value);
        Assert.Equal("%_%_3_%", opts[2].Value);
    }

    [Fact]
    public void DropOptions_FiltersByMaxQualityAndEncodesValue() {
        var arts = new[]
        {
            new PossibleArtifact { Name = 40, ProtoName = "BOOK_OF_BASAN", DisplayName = "Book of Basan", Level = 1, Rarity = 0, BaseQuality = 3 },
            new PossibleArtifact { Name = 41, ProtoName = "TOO_RARE", DisplayName = "Too Rare", Level = 0, Rarity = 0, BaseQuality = 999 },
        };
        var opts = FilterOptions.GetDropFilterOptions(arts, maxQuality: 100, advanced: false);
        // Trio + one in-range artifact (the 999-quality one is filtered out).
        Assert.Equal(4, opts.Count);
        var basan = opts[3];
        Assert.Equal("40_1_0_3", basan.Value);
        Assert.Equal("Book of Basan (T2)", basan.Text);
    }

    [Fact]
    public void DropOptions_AdvancedAddsAnyAndAnyRarityRows() {
        var arts = new[]
        {
            new PossibleArtifact { Name = 40, ProtoName = "BASAN", DisplayName = "Basan", Level = 1, Rarity = 0, BaseQuality = 3 },
            new PossibleArtifact { Name = 40, ProtoName = "BASAN", DisplayName = "Basan", Level = 1, Rarity = 1, BaseQuality = 3 },
        };
        var opts = FilterOptions.GetDropFilterOptions(arts, maxQuality: 100, advanced: true);
        // trio + family "(Any)" + tier "(Any Rarity)" + 2 concrete = 7.
        Assert.Equal(7, opts.Count);
        Assert.Equal("40_%_%_%", opts[3].Value);
        Assert.Equal("40_1_%_%", opts[4].Value);
        Assert.Equal("40_1_0_3", opts[5].Value);
        Assert.Equal("40_1_1_3", opts[6].Value);
    }
}
