using EggLedger.Domain.Eiafx;
using Ei;

namespace EggLedger.Domain.MissionPacking;

public sealed class EiafxMissionConfigSource : IMissionConfigSource {
    public static readonly EiafxMissionConfigSource Instance = new();

    public ArtifactsConfigurationResponse Config => EiafxConfig.Config;
}
