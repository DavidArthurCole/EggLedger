using EggLedger.Domain.Eiafx;
using Ei;

namespace EggLedger.Domain.MissionQuery;

/// <summary>
/// Canonical <see cref="IArtifactQuality"/> backed by the shared eiafx config;
/// delegates to <see cref="Quality.BaseQualityFor"/>.
/// </summary>
public sealed class EiafxQualityAdapter : IArtifactQuality
{
    public static readonly EiafxQualityAdapter Instance = new();

    public double BaseQualityFor(ArtifactSpec spec) => Quality.BaseQualityFor(spec);
}
