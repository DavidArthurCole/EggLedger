using Ei;

namespace EggLedger.Domain.MissionQuery;

/// <summary>
/// Resolves an artifact spec's base quality. Mirrors Go eiafx.BaseQualityFor.
/// Injected rather than referenced directly so MissionQuery does not hard-depend
/// on the concurrently ported eiafx config loader. A real implementation backs
/// this with the eiafx artifact-parameter table; tests supply a stub.
/// </summary>
public interface IArtifactQuality
{
    /// <summary>Base quality for the spec, matched by name+level+rarity. 0 if unknown.</summary>
    double BaseQualityFor(ArtifactSpec spec);
}
