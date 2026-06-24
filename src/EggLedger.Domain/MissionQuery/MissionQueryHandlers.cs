using EggLedger.Domain.Ei;
using Ei;

namespace EggLedger.Domain.MissionQuery;

/// <summary>
/// Port of Go package missionquery (missionquery.go + drops.go). Handler logic only;
/// data access via <see cref="IMissionStore"/>, base quality via <see cref="IArtifactQuality"/>. SQL not ported.
/// </summary>
public sealed class MissionQueryHandlers {
    private readonly IMissionStore _store;
    private readonly IArtifactQuality _quality;

    public MissionQueryHandlers(IMissionStore store, IArtifactQuality quality) {
        _store = store;
        _quality = quality;
    }

    /// <summary>Port of GetMissionIds. Null on store error.</summary>
    public Task<IReadOnlyList<string>?> GetMissionIdsAsync(string playerId) =>
        _store.GetCompleteMissionIdsAsync(playerId);

    /// <summary>
    /// Port of GetExistingData. Joins known accounts with their stats, dropping
    /// accounts with no stored missions or whose stats errored. Order follows the known-account list.
    /// </summary>
    public async Task<List<DatabaseAccount>> GetExistingDataAsync() {
        var result = new List<DatabaseAccount>();
        foreach (var acct in await _store.GetKnownAccountsAsync()) {
            var stats = await _store.GetPlayerMissionStatsAsync(acct.Id);
            if (stats is null) {
                continue;
            }
            if (stats.Value.Count > 0) {
                result.Add(new DatabaseAccount {
                    Id = acct.Id,
                    Nickname = acct.Nickname,
                    MissionCount = stats.Value.Count,
                    EBString = acct.EBString,
                    AccountColor = acct.AccountColor,
                    LastMissionReturnDT = stats.Value.MaxReturnTimestamp,
                });
            }
        }
        return result;
    }

    /// <summary>Port of ViewMissionsOfEid -> viewMissionsOfId. Null on error.</summary>
    public async Task<IReadOnlyList<IMissionRow>?> ViewMissionsOfEidAsync(string eid) {
        int? pending = await _store.CountPendingFilterColsAsync(eid);

        // Fast path: every mission has filter columns; build from DB columns only.
        if (pending is 0) {
            return await _store.GetPlayerMissionMetaAsync(eid);
        }

        // Slow path: decode + compile every payload.
        var complete = await _store.GetPlayerCompleteMissionsAsync(eid);
        if (complete is null) {
            return null;
        }
        var missions = new List<IMissionRow>(complete.Count);
        foreach (var cm in complete) {
            missions.Add(_store.CompileMissionInformation(cm));
        }

        // Kick a one-time background backfill so the next call uses the fast path.
        if (pending is > 0) {
            _store.QueueFilterColBackfill(eid);
        }

        return missions;
    }

    /// <summary>
    /// Port of GetDurationConfigs. Shapes eiafx mission parameters into the per-ship
    /// duration config list; the parameter table is supplied by the caller.
    /// </summary>
    public static List<PossibleMission> GetDurationConfigs(
        IEnumerable<ArtifactsConfigurationResponse.MissionParameters> missionParameters) {
        var result = new List<PossibleMission>();
        foreach (var mission in missionParameters) {
            int maxLevels = mission.LevelMissionRequirements?.Length ?? 0;
            var durations = new List<DurationConfig>();
            foreach (var d in mission.Durations) {
                durations.Add(new DurationConfig {
                    DurationType = d.DurationType,
                    MinQuality = d.MinQuality,
                    MaxQuality = d.MaxQuality,
                    LevelQualityBump = d.LevelQualityBump,
                    MaxLevels = maxLevels,
                });
            }
            result.Add(new PossibleMission { Ship = mission.Ship, Durations = durations });
        }
        return result;
    }

    /// <summary>
    /// Port of GetAllPlayerDrops. Maps missionId to drops for every stored mission,
    /// streaming one at a time. Null on store error.
    /// </summary>
    public async Task<Dictionary<string, List<MissionDrop>>?> GetAllPlayerDropsAsync(string playerId) {
        // Read pre-extracted drop rows instead of re-decoding every mission. In the
        // browser each decode is a server round-trip, so decoding a full history here
        // would mean thousands of sequential requests.
        var stored = await _store.GetStoredPlayerDropsAsync(playerId);
        if (stored is null) {
            return null;
        }

        var result = new Dictionary<string, List<MissionDrop>>();
        foreach (var d in stored) {
            var spec = new ArtifactSpec {
                name = (ArtifactSpec.Name)d.ArtifactId,
                level = (ArtifactSpec.Level)d.Level,
                rarity = (ArtifactSpec.Rarity)d.Rarity,
            };
            if (!result.TryGetValue(d.MissionId, out var drops)) {
                drops = [];
                result[d.MissionId] = drops;
            }
            drops.Add(ShapeDrop(spec));
        }
        return result;
    }

    /// <summary>Port of GetShipDrops. Drops for one mission. Null on error or cache miss.</summary>
    public async Task<List<MissionDrop>?> GetShipDropsAsync(string playerId, string missionId) {
        var cm = await _store.GetCompleteMissionAsync(playerId, missionId);
        if (cm is null) {
            return null;
        }
        var drops = new List<MissionDrop>();
        foreach (var artifact in cm.Artifacts) {
            var spec = artifact.Spec;
            if (spec is not null) {
                drops.Add(ShapeDrop(spec));
            }
        }
        return drops;
    }

    /// <summary>
    /// Shared drop-shaping logic from drops.go. Classifies SpecType by proto name;
    /// effect string attached for Stones/Artifacts only.
    /// </summary>
    private MissionDrop ShapeDrop(ArtifactSpec spec) {
        string protoName = EnumNames.ProtoName(spec.name);
        var drop = new MissionDrop {
            Id = (int)spec.name,
            Name = protoName,
            GameName = spec.CasedName(),
            Level = (int)spec.level,
            Rarity = (int)spec.rarity,
            Quality = _quality.BaseQualityFor(spec),
            IVOrder = spec.name.InventoryVisualizerOrder(),
        };

        if (protoName.Contains("_FRAGMENT", StringComparison.Ordinal)) {
            drop.SpecType = "StoneFragment";
        } else if (protoName.Contains("_STONE", StringComparison.Ordinal)) {
            drop.SpecType = "Stone";
            drop.EffectString = spec.CombinedEffectString();
        } else if (protoName.Contains("GOLD_METEORITE", StringComparison.Ordinal)
                   || protoName.Contains("SOLAR_TITANIUM", StringComparison.Ordinal)
                   || protoName.Contains("TAU_CETI_GEODE", StringComparison.Ordinal)) {
            drop.SpecType = "Ingredient";
        } else {
            drop.SpecType = "Artifact";
            drop.EffectString = spec.CombinedEffectString();
        }

        return drop;
    }
}
