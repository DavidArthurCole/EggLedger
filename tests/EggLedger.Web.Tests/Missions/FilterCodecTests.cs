using EggLedger.Web.Missions.Model;
using Xunit;
using ReportCondition = EggLedger.Domain.Reports.FilterCondition;
using ReportFilters = EggLedger.Domain.Reports.ReportFilters;

namespace EggLedger.Web.Tests.Missions;

public class FilterCodecTests
{
    private static ReportCondition R(string topLevel, string op, string val) =>
        new() { TopLevel = topLevel, Op = op, Val = val };

    [Fact]
    public void Roundtrip_SingleAndGroup_IsLossless()
    {
        var legacy = new ReportFilters
        {
            And =
            [
                R("ship", "=", "2"),
                R("level", ">", "5"),
                R("launchDT", "d=", "2024-06-01"),
                R("dubcap", "=", "true"),
            ],
        };

        var typed = FilterCodec.FromLegacy(legacy);
        var back = FilterCodec.ToLegacy(typed);

        Assert.Single(typed.Groups);
        Assert.Equal(4, typed.Groups[0].Conditions.Count);
        Assert.Equal(4, back.And.Count);
        Assert.Empty(back.Or);
        Assert.Equal("ship", back.And[0].TopLevel);
        Assert.Equal("2", back.And[0].Val);
        Assert.Equal("level", back.And[1].TopLevel);
        Assert.Equal(">", back.And[1].Op);
        Assert.Equal("launchDT", back.And[2].TopLevel);
        Assert.Equal("d=", back.And[2].Op);
        Assert.Equal("dubcap", back.And[3].TopLevel);
        Assert.Equal("true", back.And[3].Val);
    }

    [Fact]
    public void Roundtrip_OrGroups_PreserveExtraGroups()
    {
        var legacy = new ReportFilters
        {
            And = [R("ship", "=", "2")],
            Or = [[R("level", ">", "5")]],
        };

        var typed = FilterCodec.FromLegacy(legacy);
        Assert.Equal(2, typed.Groups.Count);

        var back = FilterCodec.ToLegacy(typed);
        Assert.Single(back.And);
        Assert.Single(back.Or);
        Assert.Equal("ship", back.And[0].TopLevel);
        Assert.Equal("level", back.Or[0][0].TopLevel);
    }

    [Fact]
    public void DropGlob_Roundtrip_Wildcards()
    {
        var m = FilterCodec.DecodeDropGlob("%_%_3_%");
        Assert.Null(m.Name);
        Assert.Null(m.Level);
        Assert.Equal(3, m.Rarity);
        Assert.Null(m.Quality);
        Assert.Equal("%_%_3_%", FilterCodec.EncodeDropGlob(m));
    }

    [Fact]
    public void DropGlob_Roundtrip_Exact()
    {
        var m = FilterCodec.DecodeDropGlob("40_2_1_9");
        Assert.Equal(40, m.Name);
        Assert.Equal(2, m.Level);
        Assert.Equal(1, m.Rarity);
        Assert.Equal(9d, m.Quality);
        Assert.Equal("40_2_1_9", FilterCodec.EncodeDropGlob(m));
    }

    [Fact]
    public void IncompleteOrUnknown_AreDropped()
    {
        var legacy = new ReportFilters
        {
            And =
            [
                R("", "=", "1"),          // no field
                R("nonsense", "=", "1"),  // unknown field
                R("ship", "=", "notanint"), // unparseable value
                R("ship", "=", "1"),      // valid
            ],
        };
        var typed = FilterCodec.FromLegacy(legacy);
        Assert.Single(typed.Groups);
        Assert.Single(typed.Groups[0].Conditions);
        Assert.Equal(FilterField.Ship, typed.Groups[0].Conditions[0].Field);
    }
}
