using Ei;
using EggLedger.Domain.Eiafx;

namespace EggLedger.Domain.MissionPacking;

/// <summary>
/// Canonical <see cref="IMissionConfigSource"/> backed by the shared
/// <see cref="EiafxConfig"/>. Returns the same loaded config the eiafx package
/// uses, instead of re-deserializing the embedded .bin. Resolves the A8 seam
/// reconciliation: prefer this over <see cref="EmbeddedMissionConfigSource"/>
/// when the canonical eiafx config is in play.
/// </summary>
public sealed class EiafxMissionConfigSource : IMissionConfigSource
{
    /// <summary>Shared stateless instance.</summary>
    public static readonly EiafxMissionConfigSource Instance = new();

    /// <inheritdoc />
    public ArtifactsConfigurationResponse Config => EiafxConfig.Config;
}
