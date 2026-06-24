using EggLedger.Domain.Eiafx;
using Ei;

namespace EggLedger.Domain.MissionPacking;

/// <summary>
/// Canonical <see cref="IMissionConfigSource"/> backed by the shared
/// <see cref="EiafxConfig"/> instead of re-deserializing the embedded .bin.
/// </summary>
public sealed class EiafxMissionConfigSource : IMissionConfigSource
{
    public static readonly EiafxMissionConfigSource Instance = new();

    public ArtifactsConfigurationResponse Config => EiafxConfig.Config;
}
