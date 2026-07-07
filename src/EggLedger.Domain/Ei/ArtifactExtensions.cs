using System.Globalization;
using EggLedger.Domain.LedgerData;
using Ei;

namespace EggLedger.Domain.Ei;

/// <summary>
/// Port of Go ei/artifacts.go: display + classification for ArtifactSpec and its enums. Go
/// pointer-presence (a.Name == nil) maps to the protobuf-net ShouldSerialize* guards.
/// </summary>
public static class ArtifactExtensions {
    private static LedgerDisplayData Config => LedgerData.LedgerData.Config;

    /// <summary>All caps. Use CasedName for cased version.</summary>
    public static string GameName(this ArtifactSpec.Name a) {
        string name = EnumNames.ProtoName(a).Replace("_", " ");
        switch (a) {
            case ArtifactSpec.Name.VialMartianDust:
                name = "VIAL OF MARTIAN DUST";
                break;
            case ArtifactSpec.Name.OrnateGusset:
                name = "GUSSET";
                break;
            case ArtifactSpec.Name.MercurysLens:
                name = "MERCURY'S LENS";
                break;
        }
        return name;
    }

    public static string CasedName(this ArtifactSpec.Name a) =>
        CapitalizeArtifactName(a.GameName().ToLowerInvariant());

    public static int InventoryVisualizerOrder(this ArtifactSpec.Name a) =>
        Config.InventoryVisualizerOrder.TryGetValue(EnumNames.ProtoName(a), out var order) ? order : 0;

    public static ArtifactSpec.Type ArtifactType(this ArtifactSpec.Name a) {
        if (Config.ArtifactTypes.TryGetValue(EnumNames.ProtoName(a), out var t)) {
            switch (t) {
                case "ARTIFACT":
                    return ArtifactSpec.Type.Artifact;
                case "STONE":
                    return ArtifactSpec.Type.Stone;
                case "STONE_INGREDIENT":
                    return ArtifactSpec.Type.StoneIngredient;
                case "INGREDIENT":
                    return ArtifactSpec.Type.Ingredient;
            }
        }
        return ArtifactSpec.Type.Artifact;
    }

    /// <summary>
    /// Family of the artifact, which is simply itself other than when it is a
    /// stone fragment, in which case the corresponding stone is returned.
    /// </summary>
    public static ArtifactSpec.Name Family(this ArtifactSpec.Name a) =>
        a.ArtifactType() == ArtifactSpec.Type.StoneIngredient ? a.CorrespondingStone() : a;

    /// <summary>Corresponding stone for a stone fragment. Undefined for non-fragments.</summary>
    public static ArtifactSpec.Name CorrespondingStone(this ArtifactSpec.Name a) {
        if (Config.StoneFragmentMap.TryGetValue(EnumNames.ProtoName(a), out var stone)
            && EnumNames.TryValue<ArtifactSpec.Name>(stone, out var val)) {
            return val;
        }
        return ArtifactSpec.Name.Unknown;
    }

    /// <summary>Corresponding stone fragment for a stone. Undefined for non-stones.</summary>
    public static ArtifactSpec.Name CorrespondingFragment(this ArtifactSpec.Name a) {
        string target = EnumNames.ProtoName(a);
        foreach (var (fragment, stone) in Config.StoneFragmentMap) {
            if (stone == target && EnumNames.TryValue<ArtifactSpec.Name>(fragment, out var val)) {
                return val;
            }
        }
        return ArtifactSpec.Name.Unknown;
    }

    public static string GenericBenefitString(this ArtifactSpec a) {
        if (!a.ShouldSerializename()) {
            return "";
        }
        return Config.GenericBenefitStrings.TryGetValue(EnumNames.ProtoName(a.name), out var s) ? s : "";
    }

    public static string DropEffectString(this ArtifactSpec a) {
        var v = EffectTableValue(a);
        return v == "" ? "" : "[" + v + "]";
    }

    /// <summary>Looks up the effect-table value for this artifact's level/rarity; "" when absent.</summary>
    private static string EffectTableValue(ArtifactSpec a) {
        if (!a.ShouldSerializename()) {
            return "";
        }
        if (!Config.ArtifactEffects.TryGetValue(EnumNames.ProtoName(a.name), out var effects)
            || !a.ShouldSerializelevel() || (int)a.level >= effects.Length) {
            return "";
        }
        var row = effects[(int)a.level];
        if (!a.ShouldSerializerarity() || (int)a.rarity >= row.Length) {
            return "";
        }
        return row[(int)a.rarity];
    }

    /// <summary>
    /// Combines the artifact's specific value with its generic benefit description. Special !! values
    /// are returned as-is; normal values substitute into the generic template where [^b] appears.
    /// </summary>
    public static string CombinedEffectString(this ArtifactSpec a) {
        var v = EffectTableValue(a);
        if (v == "") {
            return "";
        }
        if (v.StartsWith("!!", StringComparison.Ordinal)) {
            return v;
        }
        var generic = a.GenericBenefitString();
        if (generic != "") {
            if (generic.Contains("[^b]", StringComparison.Ordinal)) {
                return ReplaceFirst(generic, "[^b]", "[" + v + "]");
            }
            if (generic.Contains("[+^b]", StringComparison.Ordinal)) {
                return ReplaceFirst(generic, "[+^b]", "[+" + v + "]");
            }
            if (generic.Contains("[-^b]", StringComparison.Ordinal)) {
                return ReplaceFirst(generic, "[-^b]", "[-" + v + "]");
            }
        }
        return "[" + v + "]";
    }

    public static string DisplayTierName(this ArtifactSpec a, bool includeSpace) {
        var tierName = a.TierName();
        if (tierName == "REGULAR") {
            return "";
        }
        return includeSpace ? tierName + " " : tierName;
    }

    /// <summary>All caps. Use CasedName for cased version.</summary>
    public static string GameName(this ArtifactSpec a) {
        string baseName = "";
        switch (a.name) {
            // Artifacts
            case ArtifactSpec.Name.LunarTotem:
                baseName = "LUNAR TOTEM";
                break;
            case ArtifactSpec.Name.NeodymiumMedallion:
                baseName = "NEODYMIUM MEDALLION";
                break;
            case ArtifactSpec.Name.BeakOfMidas:
                baseName = "BEAK OF MIDAS";
                break;
            case ArtifactSpec.Name.LightOfEggendil:
                baseName = "LIGHT OF EGGENDIL";
                break;
            case ArtifactSpec.Name.DemetersNecklace:
                baseName = "DEMETERS NECKLACE";
                break;
            case ArtifactSpec.Name.VialMartianDust:
                baseName = "VIAL OF MARTIAN DUST";
                break;
            case ArtifactSpec.Name.OrnateGusset:
                baseName = "ORNATE GUSSET";
                break;
            case ArtifactSpec.Name.TheChalice:
                baseName = "CHALICE";
                break;
            case ArtifactSpec.Name.BookOfBasan:
                baseName = "BOOK OF BASAN";
                break;
            case ArtifactSpec.Name.PhoenixFeather:
                baseName = "PHOENIX FEATHER";
                break;
            case ArtifactSpec.Name.TungstenAnkh:
                baseName = "TUNGSTEN ANKH";
                break;
            case ArtifactSpec.Name.AurelianBrooch:
                baseName = "AURELIAN BROOCH";
                break;
            case ArtifactSpec.Name.CarvedRainstick:
                baseName = "CARVED RAINSTICK";
                break;
            case ArtifactSpec.Name.PuzzleCube:
                baseName = "PUZZLE CUBE";
                break;
            case ArtifactSpec.Name.QuantumMetronome:
                baseName = "QUANTUM METRONOME";
                break;
            case ArtifactSpec.Name.ShipInABottle:
                baseName = "SHIP IN A BOTTLE";
                break;
            case ArtifactSpec.Name.TachyonDeflector:
                baseName = "TACHYON DEFLECTOR";
                break;
            case ArtifactSpec.Name.InterstellarCompass:
                baseName = "INTERSTELLAR COMPASS";
                break;
            case ArtifactSpec.Name.DilithiumMonocle:
                baseName = "DILITHIUM MONOCLE";
                break;
            case ArtifactSpec.Name.TitaniumActuator:
                baseName = "TITANIUM ACTUATOR";
                break;
            case ArtifactSpec.Name.MercurysLens:
                baseName = "MERCURY'S LENS";
                break;
            // Stones
            case ArtifactSpec.Name.TachyonStone:
                baseName = "TACHYON STONE";
                break;
            case ArtifactSpec.Name.DilithiumStone:
                baseName = "DILITHIUM STONE";
                break;
            case ArtifactSpec.Name.ShellStone:
                baseName = "SHELL STONE";
                break;
            case ArtifactSpec.Name.LunarStone:
                baseName = "LUNAR STONE";
                break;
            case ArtifactSpec.Name.SoulStone:
                baseName = "SOUL STONE";
                break;
            case ArtifactSpec.Name.ProphecyStone:
                baseName = "PROPHECY STONE";
                break;
            case ArtifactSpec.Name.QuantumStone:
                baseName = "QUANTUM STONE";
                break;
            case ArtifactSpec.Name.TerraStone:
                baseName = "TERRA STONE";
                break;
            case ArtifactSpec.Name.LifeStone:
                baseName = "LIFE STONE";
                break;
            case ArtifactSpec.Name.ClarityStone:
                baseName = "CLARITY STONE";
                break;
            // Stone fragments
            case ArtifactSpec.Name.TachyonStoneFragment:
            case ArtifactSpec.Name.DilithiumStoneFragment:
            case ArtifactSpec.Name.ShellStoneFragment:
            case ArtifactSpec.Name.LunarStoneFragment:
            case ArtifactSpec.Name.SoulStoneFragment:
            case ArtifactSpec.Name.ProphecyStoneFragment:
            case ArtifactSpec.Name.QuantumStoneFragment:
            case ArtifactSpec.Name.TerraStoneFragment:
            case ArtifactSpec.Name.LifeStoneFragment:
            case ArtifactSpec.Name.ClarityStoneFragment:
                return EnumNames.ProtoName(a.name).Replace("_", " ");
            // Ingredients
            case ArtifactSpec.Name.GoldMeteorite:
                switch (a.level) {
                    case ArtifactSpec.Level.Inferior:
                        return "TINY GOLD METEORITE";
                    case ArtifactSpec.Level.Lesser:
                        return "ENRICHED GOLD METEORITE";
                    case ArtifactSpec.Level.Normal:
                        return "SOLID GOLD METEORITE";
                }
                break;
            case ArtifactSpec.Name.TauCetiGeode:
                switch (a.level) {
                    case ArtifactSpec.Level.Inferior:
                        return "TAU CETI GEODE PIECE";
                    case ArtifactSpec.Level.Lesser:
                        return "GLIMMERING TAU CETI GEODE";
                    case ArtifactSpec.Level.Normal:
                        return "RADIANT TAU CETI GEODE";
                }
                break;
            case ArtifactSpec.Name.SolarTitanium:
                switch (a.level) {
                    case ArtifactSpec.Level.Inferior:
                        return "SOLAR TITANIUM ORE";
                    case ArtifactSpec.Level.Lesser:
                        return "SOLAR TITANIUM BAR";
                    case ArtifactSpec.Level.Normal:
                        return "SOLAR TITANIUM GEOGON";
                }
                break;
            // Unconfirmed ingredients
            case ArtifactSpec.Name.ExtraterrestrialAluminum:
            case ArtifactSpec.Name.AncientTungsten:
            case ArtifactSpec.Name.SpaceRocks:
            case ArtifactSpec.Name.AlienWood:
            case ArtifactSpec.Name.CentaurianSteel:
            case ArtifactSpec.Name.EridaniFeather:
            case ArtifactSpec.Name.DroneParts:
            case ArtifactSpec.Name.CelestialBronze:
            case ArtifactSpec.Name.LalandeHide:
                return "? " + EnumNames.ProtoName(a.name);
        }

        return a.DisplayTierName(true) + baseName;
    }

    public static string CasedName(this ArtifactSpec a) =>
        CapitalizeArtifactName(a.GameName().ToLowerInvariant());

    public static string CasedSmallName(this ArtifactSpec a) =>
        CapitalizeArtifactName(EnumNames.ProtoName(a.name).Replace("_", " ").ToLowerInvariant());

    public static ArtifactSpec.Type Type(this ArtifactSpec a) => a.name.ArtifactType();

    /// <summary>
    /// Family the artifact belongs to, which is the corresponding stone for
    /// stone fragments.
    /// </summary>
    public static ArtifactSpec.Name Family(this ArtifactSpec a) => a.name.Family();

    public static int TierNumber(this ArtifactSpec a) {
        return a.Type() switch {
            ArtifactSpec.Type.Artifact => (int)a.level + 1,
            ArtifactSpec.Type.Stone => (int)a.level + 2,
            ArtifactSpec.Type.StoneIngredient => 1,
            ArtifactSpec.Type.Ingredient => (int)a.level + 1,
            _ => 1,
        };
    }

    public static string TierName(this ArtifactSpec a) {
        if (!a.ShouldSerializename() || !a.ShouldSerializelevel()) {
            return "?";
        }
        if (Config.ArtifactTierNames.TryGetValue(EnumNames.ProtoName(a.name), out var names)) {
            int idx = (int)a.level;
            if (idx >= 0 && idx < names.Length) {
                return names[idx];
            }
        }
        return "?";
    }

    public static string CasedTierName(this ArtifactSpec a) =>
        CultureInfo.GetCultureInfo("en-US").TextInfo.ToTitleCase(a.TierName().ToLowerInvariant());

    public static string Display(this ArtifactSpec a) {
        string s = $"{a.CasedName()} (T{a.TierNumber()})";
        if ((int)a.rarity > 0) {
            s += $", {a.rarity.Display()}";
        }
        return s;
    }

    public static string Display(this ArtifactSpec.Rarity r) {
        return r switch {
            ArtifactSpec.Rarity.Common => "Common",
            ArtifactSpec.Rarity.Rare => "Rare",
            ArtifactSpec.Rarity.Epic => "Epic",
            ArtifactSpec.Rarity.Legendary => "Legendary",
            _ => "Unknown",
        };
    }

    public static string Display(this ArtifactSpec.Type t) =>
        new[] { "Artifact", "Stone", "Ingredient", "Stone ingredient" }[(int)t];

    private static string CapitalizeArtifactName(string n) {
        n = char.ToUpperInvariant(n[0]) + n[1..];
        // Capitalize proper nouns.
        var replacements = new (string from, string to)[]
        {
            ("demeters", "Demeters"),
            ("midas", "Midas"),
            ("eggendil", "Eggendil"),
            ("martian", "Martian"),
            ("basan", "Basan"),
            ("aurelian", "Aurelian"),
            ("mercury", "Mercury"),
            ("tau ceti", "Tau Ceti"),
            ("Tau ceti", "Tau Ceti"),
        };
        foreach (var (from, to) in replacements) {
            n = n.Replace(from, to);
        }
        return n;
    }

    private static string ReplaceFirst(string source, string oldValue, string newValue) {
        int idx = source.IndexOf(oldValue, StringComparison.Ordinal);
        if (idx < 0) {
            return source;
        }
        return string.Concat(source.AsSpan(0, idx), newValue, source.AsSpan(idx + oldValue.Length));
    }
}
