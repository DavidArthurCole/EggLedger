using Ei;

namespace EggLedger.Domain.MissionPacking;

public interface IMissionConfigSource {
    ArtifactsConfigurationResponse Config { get; }
}
