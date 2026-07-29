using Ei;

namespace EggLedger.Domain.MissionQuery;

public interface IArtifactQuality {
    double BaseQualityFor(ArtifactSpec spec);
}
