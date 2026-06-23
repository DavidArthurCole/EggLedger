using Ei;
using EggLedger.Domain.Ei;
using EggLedger.Domain.Eiafx;

namespace EggLedger.Domain.MissionPacking;

/// <summary>
/// One artifact drop ready for persistence. C# port of the per-row shape Go
/// db.BuildArtifactDropRows produces. The persistence layer maps this to its
/// storage row.
/// </summary>
public readonly record struct ArtifactDrop(
    int DropIndex,
    int ArtifactId,
    string SpecType,
    int Level,
    int Rarity,
    double Quality);

/// <summary>
/// Builds artifact-drop rows from a decoded mission. C# port of Go
/// db.BuildArtifactDropRows: drop_index is the 0-based position, spec_type is
/// classified by proto name, quality is the eiafx base quality.
/// </summary>
public static class ArtifactDrops
{
    public static List<ArtifactDrop> Build(CompleteMissionResponse resp)
    {
        ArgumentNullException.ThrowIfNull(resp);
        var artifacts = resp.Artifacts;
        var drops = new List<ArtifactDrop>(artifacts.Count);
        for (int i = 0; i < artifacts.Count; i++)
        {
            var spec = artifacts[i].Spec!;
            string name = EnumNames.ProtoName(spec.name);
            string specType;
            if (name.Contains("_FRAGMENT", StringComparison.Ordinal))
            {
                specType = "StoneFragment";
            }
            else if (name.Contains("_STONE", StringComparison.Ordinal))
            {
                specType = "Stone";
            }
            else if (name.Contains("GOLD_METEORITE", StringComparison.Ordinal)
                     || name.Contains("SOLAR_TITANIUM", StringComparison.Ordinal)
                     || name.Contains("TAU_CETI_GEODE", StringComparison.Ordinal))
            {
                specType = "Ingredient";
            }
            else
            {
                specType = "Artifact";
            }

            drops.Add(new ArtifactDrop(
                DropIndex: i,
                ArtifactId: (int)spec.name,
                SpecType: specType,
                Level: (int)spec.level,
                Rarity: (int)spec.rarity,
                Quality: Quality.BaseQualityFor(spec)));
        }
        return drops;
    }
}
