using Ei;

namespace EggLedger.Domain.MissionPacking;

/// <summary>
/// Source of eiafx mission-parameter data for the nominal-capacity table.
/// Decouples MissionPacking from the concrete eiafx config loader.
/// </summary>
public interface IMissionConfigSource {
    ArtifactsConfigurationResponse Config { get; }
}
