using EggLedger.Domain.MissionPacking;
using EggLedger.Web.Missions;

namespace EggLedger.Web.Tests.Missions;

/// <summary>
/// Golden tests for <see cref="MissionGrouper"/> derived from
/// www/src/composables/useMissionListGrouping.ts: year/month/day descending
/// grouping, per-day reverse, collapse state, and the all-visible flag.
/// </summary>
public sealed class MissionGrouperTests {
    // Deterministic ledgerDate that treats LaunchDT as already-encoded yyyymmdd
    // so tests do not depend on the local time zone.
    private static DateTime FakeLedgerDate(long encoded) {
        int y = (int)(encoded / 10000);
        int mo = (int)(encoded / 100 % 100);
        int d = (int)(encoded % 100);
        return new DateTime(y, mo, d, 0, 0, 0, DateTimeKind.Local);
    }

    private static DatabaseMission M(long encoded, string id) =>
        new() { LaunchDT = encoded, MissiondId = id };

    [Fact]
    public void EmptyInput_ReturnsAllVisibleEmpty() {
        var g = MissionGrouper.Group(Array.Empty<DatabaseMission>(), FakeLedgerDate, collapseOlderSections: false);
        Assert.Empty(g.Missions);
        Assert.True(g.AllVisible);
    }

    [Fact]
    public void Null_ReturnsAllVisibleEmpty() {
        var g = MissionGrouper.Group(null, FakeLedgerDate, collapseOlderSections: false);
        Assert.Empty(g.Missions);
        Assert.True(g.AllVisible);
    }

    [Fact]
    public void YearsSortedDescending() {
        var missions = new[]
        {
            M(20220101, "a"),
            M(20240101, "b"),
            M(20230101, "c"),
        };
        var g = MissionGrouper.Group(missions, FakeLedgerDate, collapseOlderSections: false);
        Assert.Equal([2024, 2023, 2022], [.. g.Arrays.Year.Select(y => y.Year)]);
    }

    [Fact]
    public void MonthsAndDaysSortedDescending() {
        var missions = new[]
        {
            M(20240101, "a"),
            M(20240315, "b"),
            M(20240310, "c"),
        };
        var g = MissionGrouper.Group(missions, FakeLedgerDate, collapseOlderSections: false);
        Assert.Equal([3, 1], [.. g.Arrays.Month[0].Select(m => m.Month)]);
        // March has two days 15 then 10.
        Assert.Equal([15, 10], [.. g.Arrays.Day[0][0].Select(d => d.Day)]);
    }

    [Fact]
    public void SameDayMissions_AreReversed() {
        var missions = new[]
        {
            M(20240101, "first"),
            M(20240101, "second"),
            M(20240101, "third"),
        };
        var g = MissionGrouper.Group(missions, FakeLedgerDate, collapseOlderSections: false);
        var dayList = g.Missions[0][0][0];
        Assert.Equal(new[] { "third", "second", "first" }, dayList.Select(m => m.MissiondId).ToArray());
    }

    [Fact]
    public void Collapse_EnablesOnlyNewestYear() {
        var missions = new[]
        {
            M(20240101, "a"),
            M(20230101, "b"),
        };
        var g = MissionGrouper.Group(missions, FakeLedgerDate, collapseOlderSections: true);
        Assert.True(g.Arrays.Year[0].Enabled);
        Assert.False(g.Arrays.Year[1].Enabled);
        Assert.True(g.Arrays.Month[0][0].Enabled);
        Assert.False(g.Arrays.Month[1][0].Enabled);
        Assert.False(g.AllVisible);
    }

    [Fact]
    public void Collapse_SingleYear_StillAllVisible() {
        var missions = new[] { M(20240101, "a"), M(20240202, "b") };
        var g = MissionGrouper.Group(missions, FakeLedgerDate, collapseOlderSections: true);
        Assert.True(g.AllVisible);
        Assert.True(g.Arrays.Year[0].Enabled);
    }

    [Fact]
    public void NoCollapse_EverythingEnabled() {
        var missions = new[] { M(20240101, "a"), M(20230101, "b") };
        var g = MissionGrouper.Group(missions, FakeLedgerDate, collapseOlderSections: false);
        Assert.True(g.Arrays.Year.All(y => y.Enabled));
        Assert.True(g.AllVisible);
    }
}
