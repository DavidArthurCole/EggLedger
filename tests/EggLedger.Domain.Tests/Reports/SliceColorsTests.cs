using EggLedger.Domain.Reports.Charts;

namespace EggLedger.Domain.Tests.Reports;

/// <summary>
/// Golden tests for the slice-color math. Reference values produced by running
/// the Vue useSliceColors.ts functions in Node against the same inputs.
/// </summary>
public class SliceColorsTests {
    [Fact]
    public void HexToHsl_MatchesVue() {
        var (h, s, l) = SliceColors.HexToHsl("#6366f1");
        Assert.Equal(238.732, h, 3);
        Assert.Equal(0.83529, s, 4);
        Assert.Equal(0.66667, l, 4);
    }

    [Fact]
    public void HslToHex_RoundTrips() {
        var (h, s, l) = SliceColors.HexToHsl("#6366f1");
        Assert.Equal("#6366f1", SliceColors.HslToHex(h, s, l));
    }

    [Fact]
    public void AutoSliceColors_FourOfIndigo_MatchesVue() {
        var colors = SliceColors.AutoSliceColors("#6366f1", 4);
        Assert.Equal(new[] { "#6366f1", "#f163ad", "#f1ee63", "#63f1a7" }, colors);
    }

    [Fact]
    public void AutoSliceColors_ThreeOfRed_AreRgbPrimaries() {
        var colors = SliceColors.AutoSliceColors("#ff0000", 3);
        Assert.Equal(new[] { "#ff0000", "#00ff00", "#0000ff" }, colors);
    }

    [Fact]
    public void GetLabelColor_PrefersOverride() {
        var labels = new[] { "A", "B", "C" };
        var map = new Dictionary<string, string> { ["B"] = "#123456" };
        Assert.Equal("#123456", SliceColors.GetLabelColor("B", "#6366f1", labels, map));
    }

    [Fact]
    public void GetLabelColor_UsesAutoSlotForUnmappedLabel() {
        var labels = new[] { "A", "B", "C", "D" };
        var map = new Dictionary<string, string>();
        // Index 1 of the 4-color spread of #6366f1.
        Assert.Equal("#f163ad", SliceColors.GetLabelColor("B", "#6366f1", labels, map));
    }

    [Fact]
    public void GetLabelColor_FallsBackToBaseWhenLabelMissing() {
        var labels = new[] { "A", "B" };
        var map = new Dictionary<string, string>();
        Assert.Equal("#6366f1", SliceColors.GetLabelColor("Z", "#6366f1", labels, map));
    }

    [Fact]
    public void ParseLabelColors_HandlesBlankAndInvalid() {
        Assert.Empty(SliceColors.ParseLabelColors(""));
        Assert.Empty(SliceColors.ParseLabelColors("not json"));
        var parsed = SliceColors.ParseLabelColors("{\"A\":\"#fff\"}");
        Assert.Equal("#fff", parsed["A"]);
    }
}
