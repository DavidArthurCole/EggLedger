using EggLedger.Domain.MissionPacking;
using EggLedger.Domain.MissionQuery;

namespace EggLedger.Web.State;

public sealed class MissionDataCache {
    private readonly TimeSpan _ttl;
    private (string AccountId, IReadOnlyList<DatabaseMission> Missions, DateTime LoadedAt)? _missions;
    private (string AccountId, Dictionary<string, List<MissionDrop>> Drops, DateTime LoadedAt)? _drops;

    public MissionDataCache() : this(TimeSpan.FromMinutes(5)) { }



    public MissionDataCache(TimeSpan ttl) {
        _ttl = ttl;
    }

    public IReadOnlyList<DatabaseMission>? GetMissions(string accountId, DateTime now) {
        if (_missions is { } m && m.AccountId == accountId && now - m.LoadedAt < _ttl) {
            return m.Missions;
        }
        return null;
    }

    public void SetMissions(string accountId, IReadOnlyList<DatabaseMission> missions, DateTime now) {
        _missions = (accountId, missions, now);
    }

    public Dictionary<string, List<MissionDrop>>? GetDrops(string accountId, DateTime now) {
        if (_drops is { } d && d.AccountId == accountId && now - d.LoadedAt < _ttl) {
            return d.Drops;
        }
        return null;
    }

    public void SetDrops(string accountId, Dictionary<string, List<MissionDrop>> drops, DateTime now) {
        _drops = (accountId, drops, now);
    }



    public void InvalidateAll() {
        _missions = null;
        _drops = null;
    }
}
