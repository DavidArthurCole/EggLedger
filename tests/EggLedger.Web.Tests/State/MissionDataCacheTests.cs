using EggLedger.Domain.MissionPacking;
using EggLedger.Domain.MissionQuery;
using EggLedger.Web.State;

namespace EggLedger.Web.Tests.State;

public class MissionDataCacheTests {
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void GetMissions_ReturnsNull_WhenNothingCached() {
        var cache = new MissionDataCache(TimeSpan.FromMinutes(5));
        Assert.Null(cache.GetMissions("acct-1", T0));
    }

    [Fact]
    public void GetMissions_ReturnsCachedValue_WithinTtl() {
        var cache = new MissionDataCache(TimeSpan.FromMinutes(5));
        var missions = new List<DatabaseMission> { new() };
        cache.SetMissions("acct-1", missions, T0);

        var result = cache.GetMissions("acct-1", T0.AddMinutes(4));

        Assert.Same(missions, result);
    }

    [Fact]
    public void GetMissions_ReturnsNull_AfterTtlExpires() {
        var cache = new MissionDataCache(TimeSpan.FromMinutes(5));
        cache.SetMissions("acct-1", new List<DatabaseMission> { new() }, T0);

        var result = cache.GetMissions("acct-1", T0.AddMinutes(6));

        Assert.Null(result);
    }

    [Fact]
    public void GetMissions_ReturnsNull_ForDifferentAccount() {
        var cache = new MissionDataCache(TimeSpan.FromMinutes(5));
        cache.SetMissions("acct-1", new List<DatabaseMission> { new() }, T0);

        var result = cache.GetMissions("acct-2", T0);

        Assert.Null(result);
    }

    [Fact]
    public void GetDrops_ReturnsCachedValue_WithinTtl() {
        var cache = new MissionDataCache(TimeSpan.FromMinutes(5));
        var drops = new Dictionary<string, List<MissionDrop>> { ["m1"] = [] };
        cache.SetDrops("acct-1", drops, T0);

        var result = cache.GetDrops("acct-1", T0.AddMinutes(1));

        Assert.Same(drops, result);
    }

    [Fact]
    public void InvalidateAll_ClearsBothSlots() {
        var cache = new MissionDataCache(TimeSpan.FromMinutes(5));
        cache.SetMissions("acct-1", new List<DatabaseMission> { new() }, T0);
        cache.SetDrops("acct-1", [], T0);

        cache.InvalidateAll();

        Assert.Null(cache.GetMissions("acct-1", T0));
        Assert.Null(cache.GetDrops("acct-1", T0));
    }

    [Fact]
    public void MissionsAndDropsSlots_AreIndependent() {
        var cache = new MissionDataCache(TimeSpan.FromMinutes(5));
        cache.SetMissions("acct-1", new List<DatabaseMission> { new() }, T0);

        Assert.NotNull(cache.GetMissions("acct-1", T0));
        Assert.Null(cache.GetDrops("acct-1", T0));
    }
}
