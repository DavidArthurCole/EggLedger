using Ei;

namespace EggLedger.Domain.MissionPacking;

/// <summary>
/// Small consuming interface for the eiafx mission-parameter data the
/// nominal-capacity table is built from. Decouples MissionPacking from the
/// concrete eiafx config loader (ported separately as EggLedger.Domain.Eiafx).
/// </summary>
public interface IMissionConfigSource
{
    /// <summary>The loaded artifacts/mission configuration.</summary>
    ArtifactsConfigurationResponse Config { get; }
}
