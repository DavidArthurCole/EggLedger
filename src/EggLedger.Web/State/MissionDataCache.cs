using EggLedger.Domain.MissionPacking;
using EggLedger.Domain.MissionQuery;

namespace EggLedger.Web.State;

/// <summary>Per-circuit cache for Mission Data/Lifetime page fetches, keyed by account id with a staleness TTL.</summary>
public sealed class MissionDataCache {
    private readonly TimeSpan _ttl;
    private (string AccountId, IReadOnlyList<DatabaseMission> Missions, DateTime LoadedAt)? _missions;
    private (string AccountId, Dictionary<string, List<MissionDrop>> Drops, DateTime LoadedAt)? _drops;

    public MissionDataCache() : this(TimeSpan.FromMinutes(5)) { }

    // Public overload so tests can inject a short TTL instead of waiting real minutes.
    // No InternalsVisibleTo wiring exists between EggLedger.Web and its test project.
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

    // Called on account switch and on successful fetch-pipeline completion - both slots
    // invalidate together since a fetch can add missions with new drops.
    public void InvalidateAll() {
        _missions = null;
        _drops = null;
    }
}
