// Based on
// https://github.com/fanaticscripter/EggContractor/blob/3ce2cdc9ee767ecc8cbdfa4ae0ac90d248dc8694/api/computed.go#L308-L1369

package ei

import (
	"fmt"
	"strings"

	"github.com/DavidArthurCole/EggLedger/ledgerdata"
	"golang.org/x/text/cases"
	"golang.org/x/text/language"
)

// GameName is in all caps. Use CasedName for cased version.
func (a ArtifactSpec_Name) GameName() string {
	name := strings.ReplaceAll(a.String(), "_", " ")
	switch a {
	case ArtifactSpec_VIAL_MARTIAN_DUST:
		name = "VIAL OF MARTIAN DUST"
	case ArtifactSpec_ORNATE_GUSSET:
		name = "GUSSET"
	case ArtifactSpec_MERCURYS_LENS:
		name = "MERCURY'S LENS"
	}
	return name
}

func (a ArtifactSpec_Name) CasedName() string {
	return capitalizeArtifactName(strings.ToLower(a.GameName()))
}

func (a *ArtifactSpec) GenericBenefitString() string {
	if a.Name == nil {
		return ""
	}
	if s, ok := ledgerdata.Config.GenericBenefitStrings[a.Name.String()]; ok {
		return s
	}
	return ""
}

func (a *ArtifactSpec) DropEffectString() string {
	if a.Name == nil {
		return ""
	}
	effects, ok := ledgerdata.Config.ArtifactEffects[a.Name.String()]
	if !ok || a.Level == nil || int(*a.Level) >= len(effects) {
		return ""
	}
	row := effects[*a.Level]
	if a.Rarity == nil || int(*a.Rarity) >= len(row) {
		return ""
	}
	v := row[*a.Rarity]
	if v == "" {
		return ""
	}
	return "[" + v + "]"
}

// CombinedEffectString returns a display string for the tooltip that combines
// the artifact's specific value with its generic benefit description.
// Special !! values (which carry their own full display string) are returned as-is.
// Normal values are substituted into the generic benefit template where [^b] appears.
func (a *ArtifactSpec) CombinedEffectString() string {
	if a.Name == nil {
		return ""
	}
	effects, ok := ledgerdata.Config.ArtifactEffects[a.Name.String()]
	if !ok || a.Level == nil || int(*a.Level) >= len(effects) {
		return ""
	}
	row := effects[*a.Level]
	if a.Rarity == nil || int(*a.Rarity) >= len(row) {
		return ""
	}
	v := row[*a.Rarity]
	if v == "" {
		return ""
	}
	if strings.HasPrefix(v, "!!") {
		return v
	}
	generic := a.GenericBenefitString()
	if generic != "" {
		if strings.Contains(generic, "[^b]") {
			return strings.Replace(generic, "[^b]", "["+v+"]", 1)
		}
		if strings.Contains(generic, "[+^b]") {
			return strings.Replace(generic, "[+^b]", "[+"+v+"]", 1)
		}
		if strings.Contains(generic, "[-^b]") {
			return strings.Replace(generic, "[-^b]", "[-"+v+"]", 1)
		}
	}
	return "[" + v + "]"
}

func (a *ArtifactSpec) DisplayTierName(includeSpace bool) string {
	tierName := a.TierName()
	if tierName == "REGULAR" {
		return ""
	} else {
		if includeSpace {
			return tierName + " "
		} else {
			return tierName
		}
	}
}

// GameName is in all caps. Use CasedName for cased version.
func (a *ArtifactSpec) GameName() string {
	var baseName string
	switch *a.Name {
	// Artifacts
	case ArtifactSpec_LUNAR_TOTEM:
		baseName = "LUNAR TOTEM"
	case ArtifactSpec_NEODYMIUM_MEDALLION:
		baseName = "NEODYMIUM MEDALLION"
	case ArtifactSpec_BEAK_OF_MIDAS:
		baseName = "BEAK OF MIDAS"
	case ArtifactSpec_LIGHT_OF_EGGENDIL:
		baseName = "LIGHT OF EGGENDIL"
	case ArtifactSpec_DEMETERS_NECKLACE:
		baseName = "DEMETERS NECKLACE"
	case ArtifactSpec_VIAL_MARTIAN_DUST:
		baseName = "VIAL OF MARTIAN DUST"
	case ArtifactSpec_ORNATE_GUSSET:
		baseName = "ORNATE GUSSET"
	case ArtifactSpec_THE_CHALICE:
		baseName = "CHALICE"
	case ArtifactSpec_BOOK_OF_BASAN:
		baseName = "BOOK OF BASAN"
	case ArtifactSpec_PHOENIX_FEATHER:
		baseName = "PHOENIX FEATHER"
	case ArtifactSpec_TUNGSTEN_ANKH:
		baseName = "TUNGSTEN ANKH"
	case ArtifactSpec_AURELIAN_BROOCH:
		baseName = "AURELIAN BROOCH"
	case ArtifactSpec_CARVED_RAINSTICK:
		baseName = "CARVED RAINSTICK"
	case ArtifactSpec_PUZZLE_CUBE:
		baseName = "PUZZLE CUBE"
	case ArtifactSpec_QUANTUM_METRONOME:
		baseName = "QUANTUM METRONOME"
	case ArtifactSpec_SHIP_IN_A_BOTTLE:
		baseName = "SHIP IN A BOTTLE"
	case ArtifactSpec_TACHYON_DEFLECTOR:
		baseName = "TACHYON DEFLECTOR"
	case ArtifactSpec_INTERSTELLAR_COMPASS:
		baseName = "INTERSTELLAR COMPASS"
	case ArtifactSpec_DILITHIUM_MONOCLE:
		baseName = "DILITHIUM MONOCLE"
	case ArtifactSpec_TITANIUM_ACTUATOR:
		baseName = "TITANIUM ACTUATOR"
	case ArtifactSpec_MERCURYS_LENS:
		baseName = "MERCURY'S LENS"
	// Stones
	case ArtifactSpec_TACHYON_STONE:
		baseName = "TACHYON STONE"
	case ArtifactSpec_DILITHIUM_STONE:
		baseName = "DILITHIUM STONE"
	case ArtifactSpec_SHELL_STONE:
		baseName = "SHELL STONE"
	case ArtifactSpec_LUNAR_STONE:
		baseName = "LUNAR STONE"
	case ArtifactSpec_SOUL_STONE:
		baseName = "SOUL STONE"
	case ArtifactSpec_PROPHECY_STONE:
		baseName = "PROPHECY STONE"
	case ArtifactSpec_QUANTUM_STONE:
		baseName = "QUANTUM STONE"
	case ArtifactSpec_TERRA_STONE:
		baseName = "TERRA STONE"
	case ArtifactSpec_LIFE_STONE:
		baseName = "LIFE STONE"
	case ArtifactSpec_CLARITY_STONE:
		baseName = "CLARITY STONE"
	// Stone fragments
	case ArtifactSpec_TACHYON_STONE_FRAGMENT:
		fallthrough
	case ArtifactSpec_DILITHIUM_STONE_FRAGMENT:
		fallthrough
	case ArtifactSpec_SHELL_STONE_FRAGMENT:
		fallthrough
	case ArtifactSpec_LUNAR_STONE_FRAGMENT:
		fallthrough
	case ArtifactSpec_SOUL_STONE_FRAGMENT:
		fallthrough
	case ArtifactSpec_PROPHECY_STONE_FRAGMENT:
		fallthrough
	case ArtifactSpec_QUANTUM_STONE_FRAGMENT:
		fallthrough
	case ArtifactSpec_TERRA_STONE_FRAGMENT:
		fallthrough
	case ArtifactSpec_LIFE_STONE_FRAGMENT:
		fallthrough
	case ArtifactSpec_CLARITY_STONE_FRAGMENT:
		return strings.ReplaceAll(a.Name.String(), "_", " ")
	// Ingredients
	case ArtifactSpec_GOLD_METEORITE:
		switch *a.Level {
		case ArtifactSpec_INFERIOR:
			return "TINY GOLD METEORITE"
		case ArtifactSpec_LESSER:
			return "ENRICHED GOLD METEORITE"
		case ArtifactSpec_NORMAL:
			return "SOLID GOLD METEORITE"
		}
	case ArtifactSpec_TAU_CETI_GEODE:
		switch *a.Level {
		case ArtifactSpec_INFERIOR:
			return "TAU CETI GEODE PIECE"
		case ArtifactSpec_LESSER:
			return "GLIMMERING TAU CETI GEODE"
		case ArtifactSpec_NORMAL:
			return "RADIANT TAU CETI GEODE"
		}
	case ArtifactSpec_SOLAR_TITANIUM:
		switch *a.Level {
		case ArtifactSpec_INFERIOR:
			return "SOLAR TITANIUM ORE"
		case ArtifactSpec_LESSER:
			return "SOLAR TITANIUM BAR"
		case ArtifactSpec_NORMAL:
			return "SOLAR TITANIUM GEOGON"
		}
	// Unconfirmed ingredients
	case ArtifactSpec_EXTRATERRESTRIAL_ALUMINUM:
		fallthrough
	case ArtifactSpec_ANCIENT_TUNGSTEN:
		fallthrough
	case ArtifactSpec_SPACE_ROCKS:
		fallthrough
	case ArtifactSpec_ALIEN_WOOD:
		fallthrough
	case ArtifactSpec_CENTAURIAN_STEEL:
		fallthrough
	case ArtifactSpec_ERIDANI_FEATHER:
		fallthrough
	case ArtifactSpec_DRONE_PARTS:
		fallthrough
	case ArtifactSpec_CELESTIAL_BRONZE:
		fallthrough
	case ArtifactSpec_LALANDE_HIDE:
		return "? " + a.Name.String()
	}

	return a.DisplayTierName(true) + baseName
}

func (a *ArtifactSpec) CasedName() string {
	return capitalizeArtifactName(strings.ToLower(a.GameName()))
}

func (a *ArtifactSpec) CasedSmallName() string {
	return capitalizeArtifactName((strings.ToLower(strings.ReplaceAll(a.Name.String(), "_", " "))))
}

func capitalizeArtifactName(n string) string {
	n = strings.ToUpper(n[:1]) + n[1:]
	// Captalize proper nouns.
	for s, repl := range map[string]string{
		"demeters": "Demeters",
		"midas":    "Midas",
		"eggendil": "Eggendil",
		"martian":  "Martian",
		"basan":    "Basan",
		"aurelian": "Aurelian",
		"mercury":  "Mercury",
		"tau ceti": "Tau Ceti",
		"Tau ceti": "Tau Ceti",
	} {
		n = strings.ReplaceAll(n, s, repl)
	}
	return n
}

func (a *ArtifactSpec) Type() ArtifactSpec_Type {
	return a.Name.ArtifactType()
}

func (a ArtifactSpec_Name) InventoryVisualizerOrder() int {
	if order, ok := ledgerdata.Config.InventoryVisualizerOrder[a.String()]; ok {
		return order
	}
	return 0
}

func (a ArtifactSpec_Name) ArtifactType() ArtifactSpec_Type {
	if t, ok := ledgerdata.Config.ArtifactTypes[a.String()]; ok {
		switch t {
		case "ARTIFACT":
			return ArtifactSpec_ARTIFACT
		case "STONE":
			return ArtifactSpec_STONE
		case "STONE_INGREDIENT":
			return ArtifactSpec_STONE_INGREDIENT
		case "INGREDIENT":
			return ArtifactSpec_INGREDIENT
		}
	}
	return ArtifactSpec_ARTIFACT
}

// Family returns the family the artifact belongs to, which is the corresponding
// stone for stone fragments.
func (a *ArtifactSpec) Family() ArtifactSpec_Name {
	return a.Name.Family()
}

// Family returns the family of the artifact, which is simply itself other than
// when it is a stone fragment, in which case the corresponding stone is
// returned.
func (a ArtifactSpec_Name) Family() ArtifactSpec_Name {
	if a.ArtifactType() == ArtifactSpec_STONE_INGREDIENT {
		return a.CorrespondingStone()
	}
	return a
}

// CorrespondingStone returns the corresponding stone for a stone fragment.
// Result is undefined for non-stone fragments.
func (a ArtifactSpec_Name) CorrespondingStone() ArtifactSpec_Name {
	if stone, ok := ledgerdata.Config.StoneFragmentMap[a.String()]; ok {
		if val, ok2 := ArtifactSpec_Name_value[stone]; ok2 {
			return ArtifactSpec_Name(val)
		}
	}
	return ArtifactSpec_UNKNOWN
}

// CorrespondingFragment returns the corresponding stone fragment for a stone.
// Result is undefined for non-stones.
func (a ArtifactSpec_Name) CorrespondingFragment() ArtifactSpec_Name {
	target := a.String()
	for fragment, stone := range ledgerdata.Config.StoneFragmentMap {
		if stone == target {
			if val, ok := ArtifactSpec_Name_value[fragment]; ok {
				return ArtifactSpec_Name(val)
			}
		}
	}
	return ArtifactSpec_UNKNOWN
}

func (a *ArtifactSpec) TierNumber() int {
	switch a.Type() {
	case ArtifactSpec_ARTIFACT:
		// 0, 1, 2, 3 => T1, T2, T3, T4
		return int(*a.Level) + 1
	case ArtifactSpec_STONE:
		// 0, 1, 2 => T2, T3, T4 (fragment as T1)
		return int(*a.Level) + 2
	case ArtifactSpec_STONE_INGREDIENT:
		return 1
	case ArtifactSpec_INGREDIENT:
		// 0, 1, 2 => T1, T2, T3
		return int(*a.Level) + 1
	}
	return 1
}

func (a *ArtifactSpec) TierName() string {
	if a.Name == nil || a.Level == nil {
		return "?"
	}
	if names, ok := ledgerdata.Config.ArtifactTierNames[a.Name.String()]; ok {
		idx := int(*a.Level)
		if idx >= 0 && idx < len(names) {
			return names[idx]
		}
	}
	return "?"
}

func (a *ArtifactSpec) CasedTierName() string {
	caser := cases.Title(language.English)
	return caser.String(strings.ToLower(a.TierName()))
}

func (a *ArtifactSpec) Display() string {
	s := fmt.Sprintf("%s (T%d)", a.CasedName(), a.TierNumber())
	if *a.Rarity > 0 {
		s += fmt.Sprintf(", %s", a.Rarity.Display())
	}
	return s
}

func (r ArtifactSpec_Rarity) Display() string {
	switch r {
	case ArtifactSpec_COMMON:
		return "Common"
	case ArtifactSpec_RARE:
		return "Rare"
	case ArtifactSpec_EPIC:
		return "Epic"
	case ArtifactSpec_LEGENDARY:
		return "Legendary"
	}
	return "Unknown"
}

func (t ArtifactSpec_Type) Display() string {
	return []string{"Artifact", "Stone", "Ingredient", "Stone ingredient"}[t]
}
