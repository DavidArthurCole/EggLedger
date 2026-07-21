using EggLedger.Domain.Eiafx;
using Ei;

namespace EggLedger.Domain.MissionQuery;

public sealed class EiafxQualityAdapter : IArtifactQuality {
    public static readonly EiafxQualityAdapter Instance = new();

    public double BaseQualityFor(ArtifactSpec spec) => Quality.BaseQualityFor(spec);
}
