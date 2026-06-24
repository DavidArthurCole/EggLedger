using Ei;

namespace EggLedger.Domain.MissionQuery;

/// <summary>
/// Resolves an artifact spec's base quality. Mirrors Go eiafx.BaseQualityFor.
/// Injected so MissionQuery does not hard-depend on the eiafx config loader.
/// </summary>
public interface IArtifactQuality {
    /// <summary>Base quality for the spec, matched by name+level+rarity. 0 if unknown.</summary>
    double BaseQualityFor(ArtifactSpec spec);
}
