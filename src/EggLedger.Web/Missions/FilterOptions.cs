using System.Globalization;
using EggLedger.Domain.MissionQuery;

namespace EggLedger.Web.Missions;

/// <summary>Filter value-option builders, golden-matched to filterOptions.ts (option order, value encoding, dedup/sort rules). MissionFilterMatcher validates against these.</summary>
public static class FilterOptions {
    private static readonly string[] ShipNames =
    [
        "Chicken One", "Chicken Nine", "Chicken Heavy",
        "BCR", "Quintillion Chicken", "Cornish-Hen Corvette",
        "Galeggtica", "Defihent", "Voyegger", "Henerprise", "Atreggies Henliner",
    ];

    private static readonly string[] DurationNames = ["Short", "Standard", "Extended", "Tutorial"];

    public static List<FilterOption> GetShipFilterOptions() {
        var result = new List<FilterOption>(ShipNames.Length);
        for (int i = 0; i < ShipNames.Length; i++) {
            result.Add(new FilterOption { Text = ShipNames[i], Value = i.ToString(CultureInfo.InvariantCulture) });
        }
        return result;
    }

    public static List<FilterOption> GetDurationFilterOptions() {
        var result = new List<FilterOption>(DurationNames.Length);
        for (int i = 0; i < DurationNames.Length; i++) {
            result.Add(new FilterOption {
                Text = DurationNames[i],
                Value = i.ToString(CultureInfo.InvariantCulture),
                StyleClass = "text-duration-" + i,
            });
        }
        return result;
    }

    public static List<FilterOption> GetLevelFilterOptions() {
        var result = new List<FilterOption>(9);
        for (int i = 0; i < 9; i++) {
            result.Add(new FilterOption { Text = i + "★", Value = i.ToString(CultureInfo.InvariantCulture) });
        }
        return result;
    }

    public static List<FilterOption> GetMissionTypeFilterOptions() =>
    [
        new FilterOption { Text = "Home", Value = "0" },
        new FilterOption { Text = "Virtue", Value = "1" },
        new FilterOption { Text = "Unknown", Value = "-1" },
    ];

    public static List<FilterOption> GetFarmFilterOptions() =>
    [
        new FilterOption { Text = "Home", Value = "0" },
        new FilterOption { Text = "Virtue", Value = "1" },
    ];

    public static List<FilterOption> GetBoolFilterOptions() =>
    [
        new FilterOption { Text = "True", Value = "true" },
        new FilterOption { Text = "False", Value = "false" },
    ];

    public static List<FilterOption> GetTargetFilterOptions(IEnumerable<PossibleTarget> possibleTargets) {
        var result = new List<FilterOption>();
        foreach (var t in possibleTargets) {
            result.Add(new FilterOption {
                Text = t.DisplayName,
                Value = t.Id.ToString(CultureInfo.InvariantCulture),
                ImagePath = t.ImageString,
            });
        }
        return result;
    }

    public static List<FilterOption> GetArtifactRarityFilterOptions() =>
    [
        new FilterOption { Text = "Common", Value = "0" },
        new FilterOption { Text = "Rare", Value = "1", StyleClass = "text-rare" },
        new FilterOption { Text = "Epic", Value = "2", StyleClass = "text-epic" },
        new FilterOption { Text = "Legendary", Value = "3", StyleClass = "text-legendary" },
    ];

    public static List<FilterOption> GetArtifactSpecTypeFilterOptions() =>
    [
        new FilterOption { Text = "Artifact", Value = "0" },
        new FilterOption { Text = "Stone", Value = "1" },
        new FilterOption { Text = "Stone Fragment", Value = "2" },
        new FilterOption { Text = "Ingredient", Value = "3" },
    ];

    /// <summary>Value options for a mission filter field. Returns [] for target (caller supplies targets) and drops (caller has its own modal).</summary>
    public static List<FilterOption> GetMissionFilterValueOptions(string topLevel) => topLevel switch {
        "ship" => GetShipFilterOptions(),
        "farm" => GetFarmFilterOptions(),
        "duration" => GetDurationFilterOptions(),
        "level" => GetLevelFilterOptions(),
        "type" => GetMissionTypeFilterOptions(),
        "dubcap" or "buggedcap" => GetBoolFilterOptions(),
        _ => [],
    };

    /// <summary>One option per distinct artifact level: dedup by level, sort ascending, value is the raw level int.</summary>
    public static List<FilterOption> GetArtifactTierFilterOptions(IEnumerable<PossibleArtifact> artifactConfigs) {
        var levels = new SortedSet<int>();
        foreach (var a in artifactConfigs) {
            levels.Add(a.Level);
        }
        var result = new List<FilterOption>();
        foreach (var level in levels) {
            result.Add(new FilterOption {
                Text = "Tier " + (level + 1),
                Value = level.ToString(CultureInfo.InvariantCulture),
            });
        }
        return result;
    }

    /// <summary>One option per artifact family: pick the lowest-level entry per name enum, value is the name enum, sort alphabetically by text.</summary>
    public static List<FilterOption> GetArtifactNameFilterOptions(IReadOnlyList<PossibleArtifact> artifactConfigs) {
        var representatives = new Dictionary<int, PossibleArtifact>();
        foreach (var a in artifactConfigs) {
            if (!representatives.TryGetValue(a.Name, out var existing) || a.Level < existing.Level) {
                representatives[a.Name] = a;
            }
        }
        var result = new List<FilterOption>();
        foreach (var a in representatives.Values) {
            result.Add(new FilterOption {
                Text = a.DisplayName,
                Value = a.Name.ToString(CultureInfo.InvariantCulture),
                ImagePath = DropPath(a),
            });
        }
        // Vue sorts via localeCompare; invariant culture compare orders like the live app, not raw UTF-16 ordinal.
        result.Sort((x, y) => string.Compare(x.Text, y.Text, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    private static string ArtifactDisplayText(PossibleArtifact artifact) {
        string displayName = artifact.DisplayName;
        int level = artifact.Level;
        string displayText = displayName;
        bool isStoneNotFragment =
            displayName.Contains("stone", StringComparison.OrdinalIgnoreCase) &&
            !displayName.Contains("fragment", StringComparison.OrdinalIgnoreCase);
        displayText += " (T" + (level + (isStoneNotFragment ? 2 : 1)) + ")";
        return displayText;
    }

    private static string DropPath(PossibleArtifact drop) {
        int addendum = drop.ProtoName.Contains("_STONE", StringComparison.Ordinal) ? 1 : 0;
        string fixedName = drop.ProtoName
            .Replace("_FRAGMENT", "", StringComparison.Ordinal)
            .Replace("ORNATE_GUSSET", "GUSSET", StringComparison.Ordinal)
            .Replace("VIAL_MARTIAN_DUST", "VIAL_OF_MARTIAN_DUST", StringComparison.Ordinal);
        return "artifacts/" + fixedName + "/" + fixedName + "_" + (drop.Level + 1 + addendum) + ".png";
    }

    private static string DropRarityPath(PossibleArtifact drop) => drop.Rarity switch {
        1 => "images/rare.gif",
        2 => "images/epic.gif",
        3 => "images/legendary.gif",
        _ => "",
    };

    /// <summary>Insertion order preserved: "Any Rare/Epic/Legendary" trio first, then per family/tier in first-seen order. Value encoding is name_level_rarity_baseQuality, "%" marks a wildcard segment.</summary>
    public static List<FilterOption> GetDropFilterOptions(
        IReadOnlyList<PossibleArtifact> artifactConfigs,
        double maxQuality,
        bool advanced) {
        var artifactList = new List<PossibleArtifact>();
        foreach (var a in artifactConfigs) {
            if (a.BaseQuality <= maxQuality) {
                artifactList.Add(a);
            }
        }

        var result = new List<FilterOption>
        {
            new() { Text = "Any Rare", Value = "%_%_1_%", Rarity = 1, StyleClass = "text-rare", ImagePath = "icon_help.webp" },
            new() { Text = "Any Epic", Value = "%_%_2_%", Rarity = 2, StyleClass = "text-epic", ImagePath = "icon_help.webp" },
            new() { Text = "Any Legendary", Value = "%_%_3_%", Rarity = 3, StyleClass = "text-legendary", ImagePath = "icon_help.webp" },
        };

        // Insertion-ordered family -> tier -> artifacts grouping (mirrors JS Map order).
        var byFamily = new Dictionary<int, Dictionary<int, List<PossibleArtifact>>>();
        var familyOrder = new List<int>();
        foreach (var a in artifactList) {
            if (!byFamily.TryGetValue(a.Name, out var byTier)) {
                byTier = [];
                byFamily[a.Name] = byTier;
                familyOrder.Add(a.Name);
            }
            if (!byTier.TryGetValue(a.Level, out var rarities)) {
                rarities = [];
                byTier[a.Level] = rarities;
            }
            rarities.Add(a);
        }

        foreach (var familyId in familyOrder) {
            var tierMap = byFamily[familyId];
            // Preserve tier first-seen order (JS Map iteration order).
            var tierOrder = TierOrder(artifactList, familyId);

            if (advanced) {
                var firstArtifact = tierMap[tierOrder[0]][0];
                result.Add(new FilterOption {
                    Text = firstArtifact.DisplayName + " (Any)",
                    Value = familyId + "_%_%_%",
                    Rarity = 0,
                    ImagePath = DropPath(firstArtifact),
                });
            }

            foreach (var tierLevel in tierOrder) {
                var rarities = tierMap[tierLevel];
                if (advanced && rarities.Exists(a => a.Rarity > 0)) {
                    var tierRep = rarities.Find(a => a.Rarity == 0) ?? rarities[0];
                    result.Add(new FilterOption {
                        Text = ArtifactDisplayText(rarities[0]) + " (Any Rarity)",
                        Value = familyId + "_" + tierLevel + "_%_%",
                        Rarity = 0,
                        ImagePath = DropPath(tierRep),
                    });
                }
                foreach (var a in rarities) {
                    result.Add(new FilterOption {
                        Text = ArtifactDisplayText(a),
                        Value = a.Name + "_" + a.Level + "_" + a.Rarity + "_" + a.BaseQuality.ToString(CultureInfo.InvariantCulture),
                        Rarity = a.Rarity,
                        ImagePath = DropPath(a),
                        RarityGif = DropRarityPath(a),
                    });
                }
            }
        }

        return result;
    }

    // First-seen tier order for a family (JS Map insertion order).
    private static List<int> TierOrder(IReadOnlyList<PossibleArtifact> artifactList, int familyId) {
        var seen = new HashSet<int>();
        var order = new List<int>();
        foreach (var a in artifactList) {
            if (a.Name == familyId && seen.Add(a.Level)) {
                order.Add(a.Level);
            }
        }
        return order;
    }
}
