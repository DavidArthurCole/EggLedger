using Ei;
using EggLedger.Domain.Eiafx;

namespace EggLedger.Domain.MissionQuery;

/// <summary>
/// Canonical <see cref="IArtifactQuality"/> backed by the eiafx config:
/// delegates to <see cref="Quality.BaseQualityFor"/>. Binds MissionQuery to the
/// single shared <see cref="EiafxConfig"/> rather than a re-decoded copy.
/// </summary>
public sealed class EiafxQualityAdapter : IArtifactQuality
{
    /// <summary>Shared stateless instance.</summary>
    public static readonly EiafxQualityAdapter Instance = new();

    /// <inheritdoc />
    public double BaseQualityFor(ArtifactSpec spec) => Quality.BaseQualityFor(spec);
}
