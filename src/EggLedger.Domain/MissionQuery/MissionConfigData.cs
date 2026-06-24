using EggLedger.Domain.Ei;
using EggLedger.Domain.Eiafx;
using EggLedger.Domain.LedgerData;
using Ei;

namespace EggLedger.Domain.MissionQuery;

/// <summary>A selectable mission target. C# port of the Go main.go PossibleTarget.</summary>
public sealed class PossibleTarget {
    public string DisplayName { get; set; } = "";

    /// <summary>ei.ArtifactSpec_Name enum value, or -1 for "None (Pre 1.27)".</summary>
    public int Id { get; set; }
    public string ImageString { get; set; } = "";
}

/// <summary>A draftable artifact for the drop / name pickers. Port of Go main.go PossibleArtifact.</summary>
public sealed class PossibleArtifact {
    public int Name { get; set; }
    public string ProtoName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int Level { get; set; }
    public int Rarity { get; set; }
    public double BaseQuality { get; set; }
}

/// <summary>
/// Builds the shared mission filter configuration from the eiafx config and ledger
/// display data. Go port of main.go init helpers (getMaxQuality / initPossibleTargets / initPossibleArtifacts).
/// </summary>
public static class MissionConfigData {
    /// <summary>Port of getMaxQuality: max over (maxQuality + levelQualityBump * maxLevels).</summary>
    public static double MaxQuality() {
        double maxQuality = 0;
        foreach (var mission in EiafxConfig.Config.mission_parameters) {
            int maxLevels = mission.LevelMissionRequirements?.Length ?? 0;
            foreach (var d in mission.Durations) {
                double comped = d.MaxQuality + (d.LevelQualityBump * maxLevels);
                if (comped > maxQuality) {
                    maxQuality = comped;
                }
            }
        }
        return maxQuality;
    }

    /// <summary>
    /// Port of initPossibleTargets: a "None (Pre 1.27)" sentinel then one entry per
    /// ledger artifact target, id from the proto enum value.
    /// </summary>
    public static List<PossibleTarget> Targets() {
        var result = new List<PossibleTarget>
        {
            new() { DisplayName = "None (Pre 1.27)", Id = -1, ImageString = "none.png" },
        };
        foreach (var target in LedgerData.LedgerData.Config.ArtifactTargets) {
            int id = EnumNames.TryValue<ArtifactSpec.Name>(target.Name, out var val) ? (int)val : 0;
            result.Add(new PossibleTarget {
                DisplayName = target.DisplayName,
                Id = id,
                ImageString = target.ImageString,
            });
        }
        return result;
    }

    /// <summary>Port of initPossibleArtifacts: every artifact param within max quality, names resolved.</summary>
    public static List<PossibleArtifact> Artifacts() {
        double maxQuality = MaxQuality();
        var result = new List<PossibleArtifact>();
        foreach (var artifact in EiafxConfig.Config.artifact_parameters) {
            var spec = artifact.Spec;
            if (spec is null) {
                continue;
            }
            if (maxQuality >= artifact.BaseQuality) {
                result.Add(new PossibleArtifact {
                    Name = (int)spec.name,
                    ProtoName = EnumNames.ProtoName(spec.name),
                    DisplayName = spec.CasedSmallName(),
                    Level = (int)spec.level,
                    Rarity = (int)spec.rarity,
                    BaseQuality = artifact.BaseQuality,
                });
            }
        }
        return result;
    }
}
