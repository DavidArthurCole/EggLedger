using EggLedger.Domain.Eiafx;
using EggLedger.Domain.MissionQuery;

namespace EggLedger.Web.Missions;

/// <summary>
/// Supplies the shared, read-only mission configuration the filter UI and the
/// <see cref="MissionFilterMatcher"/> need: per-ship duration configs, possible
/// targets, draftable artifacts, and the global max quality. Thin scoped wrapper
/// caching the Domain <see cref="MissionConfigData"/> builders (themselves ports
/// of the Go main.go init helpers + missionquery.GetDurationConfigs).
/// </summary>
public sealed class MissionConfigProvider
{
    private readonly Lazy<IReadOnlyList<PossibleMission>> _durationConfigs =
        new(() => MissionQueryHandlers.GetDurationConfigs(EiafxConfig.Config.mission_parameters));

    private readonly Lazy<double> _maxQuality = new(MissionConfigData.MaxQuality);
    private readonly Lazy<IReadOnlyList<PossibleTarget>> _targets = new(() => MissionConfigData.Targets());
    private readonly Lazy<IReadOnlyList<PossibleArtifact>> _artifacts = new(() => MissionConfigData.Artifacts());

    public IReadOnlyList<PossibleMission> DurationConfigs => _durationConfigs.Value;
    public double MaxQuality => _maxQuality.Value;
    public IReadOnlyList<PossibleTarget> PossibleTargets => _targets.Value;
    public IReadOnlyList<PossibleArtifact> PossibleArtifacts => _artifacts.Value;

    /// <summary>Filter field context built from the active config.</summary>
    public FilterFieldCtx FieldCtx => new()
    {
        PossibleTargets = PossibleTargets,
        ArtifactConfigs = PossibleArtifacts,
        MaxQuality = MaxQuality,
    };
}
