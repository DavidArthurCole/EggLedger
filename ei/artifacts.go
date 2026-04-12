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
	switch *a.Name {
	case ArtifactSpec_LUNAR_TOTEM:
		return "[^b] away earnings"
	case ArtifactSpec_NEODYMIUM_MEDALLION:
		return "[+^b] drone frequency"
	case ArtifactSpec_BEAK_OF_MIDAS:
		return "[+^b] chance of gold in gifts and drones"
	case ArtifactSpec_LIGHT_OF_EGGENDIL:
		return "Enlightenment egg value increased by [^b]"
	case ArtifactSpec_TUNGSTEN_ANKH:
		fallthrough
	case ArtifactSpec_DEMETERS_NECKLACE:
		return "[+^b] egg value"
	case ArtifactSpec_VIAL_MARTIAN_DUST:
		return "Increases max running chicken bonus by [^b]"
	case ArtifactSpec_ORNATE_GUSSET:
		return "[+^b] hab capacity"
	case ArtifactSpec_THE_CHALICE:
		return "[+^b] internal hatchery rate"
	case ArtifactSpec_BOOK_OF_BASAN:
		return "[+^b] to Egg of Prophecy bonus"
	case ArtifactSpec_PHOENIX_FEATHER:
		return "[+^b] Soul Egg collection rate"
	case ArtifactSpec_AURELIAN_BROOCH:
		return "[+^b] drone rewards"
	case ArtifactSpec_CARVED_RAINSTICK:
		return "[+^b] chance of cash in gifts and drones"
	case ArtifactSpec_PUZZLE_CUBE:
		return "[-^b] research cost"
	case ArtifactSpec_QUANTUM_METRONOME:
		return "[+^b] egg laying rate"
	case ArtifactSpec_SHIP_IN_A_BOTTLE:
		return "[+^b] co-op teammate's earnings"
	case ArtifactSpec_TACHYON_DEFLECTOR:
		return "[+^b] co-op teammate's egg laying rate"
	case ArtifactSpec_INTERSTELLAR_COMPASS:
		return "[+^b] shipping rate"
	case ArtifactSpec_DILITHIUM_MONOCLE:
		return "[+^b] boost boost"
	case ArtifactSpec_TITANIUM_ACTUATOR:
		return "[+^b] hold to hatch rate"
	case ArtifactSpec_MERCURYS_LENS:
		return "[+^b] farm valuation"
	// Stones
	case ArtifactSpec_TACHYON_STONE:
		return "[+^b] egg laying rate"
	case ArtifactSpec_DILITHIUM_STONE:
		return "[+^b] boost duration"
	case ArtifactSpec_SHELL_STONE:
		return "[+^b] egg value"
	case ArtifactSpec_LUNAR_STONE:
		return "[+^b] away earnings"
	case ArtifactSpec_SOUL_STONE:
		return "[+^b] bonus per Soul Egg"
	case ArtifactSpec_PROPHECY_STONE:
		return "[+^b] to Egg of Prophecy bonus"
	case ArtifactSpec_QUANTUM_STONE:
		return "[+^b] shipping rate"
	case ArtifactSpec_TERRA_STONE:
		return "Increases max running chicken bonus by [^b]"
	case ArtifactSpec_LIFE_STONE:
		return "[+^b] internal hatchery rate"
	case ArtifactSpec_CLARITY_STONE:
		return "[^b] effect of host artifact on enlightenment egg farms"
	default:
		return ""
	}
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
	return row[*a.Rarity]
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
	switch a {
	case ArtifactSpec_LUNAR_TOTEM:
		return 15
	case ArtifactSpec_NEODYMIUM_MEDALLION:
		return 21
	case ArtifactSpec_BEAK_OF_MIDAS:
		return 23
	case ArtifactSpec_LIGHT_OF_EGGENDIL:
		return 34
	case ArtifactSpec_DEMETERS_NECKLACE:
		return 16
	case ArtifactSpec_VIAL_MARTIAN_DUST:
		return 17
	case ArtifactSpec_ORNATE_GUSSET:
		return 20
	case ArtifactSpec_THE_CHALICE:
		return 26
	case ArtifactSpec_BOOK_OF_BASAN:
		return 33
	case ArtifactSpec_PHOENIX_FEATHER:
		return 27
	case ArtifactSpec_TUNGSTEN_ANKH:
		return 19
	case ArtifactSpec_AURELIAN_BROOCH:
		return 18
	case ArtifactSpec_CARVED_RAINSTICK:
		return 24
	case ArtifactSpec_PUZZLE_CUBE:
		return 14
	case ArtifactSpec_QUANTUM_METRONOME:
		return 28
	case ArtifactSpec_SHIP_IN_A_BOTTLE:
		return 31
	case ArtifactSpec_TACHYON_DEFLECTOR:
		return 32
	case ArtifactSpec_INTERSTELLAR_COMPASS:
		return 25
	case ArtifactSpec_DILITHIUM_MONOCLE:
		return 29
	case ArtifactSpec_TITANIUM_ACTUATOR:
		return 30
	case ArtifactSpec_MERCURYS_LENS:
		return 22
	case ArtifactSpec_TACHYON_STONE_FRAGMENT:
		fallthrough
	case ArtifactSpec_TACHYON_STONE:
		return 6
	case ArtifactSpec_DILITHIUM_STONE_FRAGMENT:
		fallthrough
	case ArtifactSpec_DILITHIUM_STONE:
		return 11
	case ArtifactSpec_SHELL_STONE_FRAGMENT:
		fallthrough
	case ArtifactSpec_SHELL_STONE:
		return 4
	case ArtifactSpec_LUNAR_STONE_FRAGMENT:
		fallthrough
	case ArtifactSpec_LUNAR_STONE:
		return 5
	case ArtifactSpec_SOUL_STONE_FRAGMENT:
		fallthrough
	case ArtifactSpec_SOUL_STONE:
		return 8
	case ArtifactSpec_PROPHECY_STONE_FRAGMENT:
		fallthrough
	case ArtifactSpec_PROPHECY_STONE:
		return 13
	case ArtifactSpec_QUANTUM_STONE_FRAGMENT:
		fallthrough
	case ArtifactSpec_QUANTUM_STONE:
		return 9
	case ArtifactSpec_TERRA_STONE_FRAGMENT:
		fallthrough
	case ArtifactSpec_TERRA_STONE:
		return 7
	case ArtifactSpec_LIFE_STONE_FRAGMENT:
		fallthrough
	case ArtifactSpec_LIFE_STONE:
		return 10
	case ArtifactSpec_CLARITY_STONE_FRAGMENT:
		fallthrough
	case ArtifactSpec_CLARITY_STONE:
		return 12
	case ArtifactSpec_GOLD_METEORITE:
		return 1
	case ArtifactSpec_TAU_CETI_GEODE:
		return 2
	case ArtifactSpec_SOLAR_TITANIUM:
		return 3
	default:
		return 0
	}
}

func (a ArtifactSpec_Name) ArtifactType() ArtifactSpec_Type {
	switch a {
	// Artifacts
	case ArtifactSpec_LUNAR_TOTEM:
		fallthrough
	case ArtifactSpec_NEODYMIUM_MEDALLION:
		fallthrough
	case ArtifactSpec_BEAK_OF_MIDAS:
		fallthrough
	case ArtifactSpec_LIGHT_OF_EGGENDIL:
		fallthrough
	case ArtifactSpec_DEMETERS_NECKLACE:
		fallthrough
	case ArtifactSpec_VIAL_MARTIAN_DUST:
		fallthrough
	case ArtifactSpec_ORNATE_GUSSET:
		fallthrough
	case ArtifactSpec_THE_CHALICE:
		fallthrough
	case ArtifactSpec_BOOK_OF_BASAN:
		fallthrough
	case ArtifactSpec_PHOENIX_FEATHER:
		fallthrough
	case ArtifactSpec_TUNGSTEN_ANKH:
		fallthrough
	case ArtifactSpec_AURELIAN_BROOCH:
		fallthrough
	case ArtifactSpec_CARVED_RAINSTICK:
		fallthrough
	case ArtifactSpec_PUZZLE_CUBE:
		fallthrough
	case ArtifactSpec_QUANTUM_METRONOME:
		fallthrough
	case ArtifactSpec_SHIP_IN_A_BOTTLE:
		fallthrough
	case ArtifactSpec_TACHYON_DEFLECTOR:
		fallthrough
	case ArtifactSpec_INTERSTELLAR_COMPASS:
		fallthrough
	case ArtifactSpec_DILITHIUM_MONOCLE:
		fallthrough
	case ArtifactSpec_TITANIUM_ACTUATOR:
		fallthrough
	case ArtifactSpec_MERCURYS_LENS:
		return ArtifactSpec_ARTIFACT
	// Stones
	case ArtifactSpec_TACHYON_STONE:
		fallthrough
	case ArtifactSpec_DILITHIUM_STONE:
		fallthrough
	case ArtifactSpec_SHELL_STONE:
		fallthrough
	case ArtifactSpec_LUNAR_STONE:
		fallthrough
	case ArtifactSpec_SOUL_STONE:
		fallthrough
	case ArtifactSpec_PROPHECY_STONE:
		fallthrough
	case ArtifactSpec_QUANTUM_STONE:
		fallthrough
	case ArtifactSpec_TERRA_STONE:
		fallthrough
	case ArtifactSpec_LIFE_STONE:
		fallthrough
	case ArtifactSpec_CLARITY_STONE:
		return ArtifactSpec_STONE
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
		return ArtifactSpec_STONE_INGREDIENT
	// Ingredients
	case ArtifactSpec_GOLD_METEORITE:
		fallthrough
	case ArtifactSpec_TAU_CETI_GEODE:
		fallthrough
	case ArtifactSpec_SOLAR_TITANIUM:
		fallthrough
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
		return ArtifactSpec_INGREDIENT
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
	switch a {
	case ArtifactSpec_TACHYON_STONE_FRAGMENT:
		return ArtifactSpec_TACHYON_STONE
	case ArtifactSpec_DILITHIUM_STONE_FRAGMENT:
		return ArtifactSpec_DILITHIUM_STONE
	case ArtifactSpec_SHELL_STONE_FRAGMENT:
		return ArtifactSpec_SHELL_STONE
	case ArtifactSpec_LUNAR_STONE_FRAGMENT:
		return ArtifactSpec_LUNAR_STONE
	case ArtifactSpec_SOUL_STONE_FRAGMENT:
		return ArtifactSpec_SOUL_STONE
	case ArtifactSpec_PROPHECY_STONE_FRAGMENT:
		return ArtifactSpec_PROPHECY_STONE
	case ArtifactSpec_QUANTUM_STONE_FRAGMENT:
		return ArtifactSpec_QUANTUM_STONE
	case ArtifactSpec_TERRA_STONE_FRAGMENT:
		return ArtifactSpec_TERRA_STONE
	case ArtifactSpec_LIFE_STONE_FRAGMENT:
		return ArtifactSpec_LIFE_STONE
	case ArtifactSpec_CLARITY_STONE_FRAGMENT:
		return ArtifactSpec_CLARITY_STONE
	}
	return ArtifactSpec_UNKNOWN
}

// CorrespondingFragment returns the corresponding stone fragment for a stone.
// Result is undefined for non-stones.
func (a ArtifactSpec_Name) CorrespondingFragment() ArtifactSpec_Name {
	switch a {
	case ArtifactSpec_TACHYON_STONE:
		return ArtifactSpec_TACHYON_STONE_FRAGMENT
	case ArtifactSpec_DILITHIUM_STONE:
		return ArtifactSpec_DILITHIUM_STONE_FRAGMENT
	case ArtifactSpec_SHELL_STONE:
		return ArtifactSpec_SHELL_STONE_FRAGMENT
	case ArtifactSpec_LUNAR_STONE:
		return ArtifactSpec_LUNAR_STONE_FRAGMENT
	case ArtifactSpec_SOUL_STONE:
		return ArtifactSpec_SOUL_STONE_FRAGMENT
	case ArtifactSpec_PROPHECY_STONE:
		return ArtifactSpec_PROPHECY_STONE_FRAGMENT
	case ArtifactSpec_QUANTUM_STONE:
		return ArtifactSpec_QUANTUM_STONE_FRAGMENT
	case ArtifactSpec_TERRA_STONE:
		return ArtifactSpec_TERRA_STONE_FRAGMENT
	case ArtifactSpec_LIFE_STONE:
		return ArtifactSpec_LIFE_STONE_FRAGMENT
	case ArtifactSpec_CLARITY_STONE:
		return ArtifactSpec_CLARITY_STONE_FRAGMENT
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
	switch *a.Name {
	// Artifacts
	case ArtifactSpec_LUNAR_TOTEM:
		return []string{"BASIC", "REGULAR", "POWERFUL", "EGGCEPTIONAL"}[*a.Level]
	case ArtifactSpec_NEODYMIUM_MEDALLION:
		return []string{"WEAK", "REGULAR", "PRECISE", "EGGCEPTIONAL"}[*a.Level]
	case ArtifactSpec_BEAK_OF_MIDAS:
		return []string{"DULL", "REGULAR", "JEWELED", "GLISTENING"}[*a.Level]
	case ArtifactSpec_LIGHT_OF_EGGENDIL:
		return []string{"DIM", "SHIMMERING", "GLOWING", "BRILLIANT"}[*a.Level]
	case ArtifactSpec_DEMETERS_NECKLACE:
		return []string{"SIMPLE", "JEWELED", "PRISTINE", "BEGGSPOKE"}[*a.Level]
	case ArtifactSpec_VIAL_MARTIAN_DUST:
		return []string{"TINY", "REGULAR", "HERMETIC", "PRIME"}[*a.Level]
	case ArtifactSpec_ORNATE_GUSSET:
		return []string{"PLAIN", "ORNATE", "DISTEGGUISHED", "JEWELED"}[*a.Level]
	case ArtifactSpec_THE_CHALICE:
		return []string{"PLAIN", "POLISHED", "JEWELED", "EGGCEPTIONAL"}[*a.Level]
	case ArtifactSpec_BOOK_OF_BASAN:
		return []string{"REGULAR", "COLLECTORS", "FORTIFIED", "GILDED"}[*a.Level]
	case ArtifactSpec_PHOENIX_FEATHER:
		return []string{"TATTERED", "REGULAR", "BRILLIANT", "BLAZING"}[*a.Level]
	case ArtifactSpec_TUNGSTEN_ANKH:
		return []string{"CRUDE", "REGULAR", "POLISHED", "BRILLIANT"}[*a.Level]
	case ArtifactSpec_AURELIAN_BROOCH:
		return []string{"PLAIN", "REGULAR", "JEWELED", "EGGCEPTIONAL"}[*a.Level]
	case ArtifactSpec_CARVED_RAINSTICK:
		return []string{"SIMPLE", "REGULAR", "ORNATE", "MEGGNIFICENT"}[*a.Level]
	case ArtifactSpec_PUZZLE_CUBE:
		return []string{"ANCIENT", "REGULAR", "MYSTICAL", "UNSOLVABLE"}[*a.Level]
	case ArtifactSpec_QUANTUM_METRONOME:
		return []string{"MISALIGNED", "ADEQUATE", "PERFECT", "REGGFERENCE"}[*a.Level]
	case ArtifactSpec_SHIP_IN_A_BOTTLE:
		return []string{"REGULAR", "DETAILED", "COMPLEX", "EGGQUISITE"}[*a.Level]
	case ArtifactSpec_TACHYON_DEFLECTOR:
		return []string{"WEAK", "REGULAR", "ROBUST", "EGGCEPTIONAL"}[*a.Level]
	case ArtifactSpec_INTERSTELLAR_COMPASS:
		return []string{"MISCALIBRATED", "REGULAR", "PRECISE", "CLAIRVOYANT"}[*a.Level]
	case ArtifactSpec_DILITHIUM_MONOCLE:
		return []string{"REGULAR", "PRECISE", "EGGSACTING", "FLAWLESS"}[*a.Level]
	case ArtifactSpec_TITANIUM_ACTUATOR:
		return []string{"INCONSISTENT", "REGULAR", "PRECISE", "REGGFERENCE"}[*a.Level]
	case ArtifactSpec_MERCURYS_LENS:
		return []string{"MISALIGNED", "REGULAR", "PRECISE", "MEGGNIFICENT"}[*a.Level]
	// Stones
	case ArtifactSpec_DILITHIUM_STONE:
		fallthrough
	case ArtifactSpec_TACHYON_STONE:
		return []string{"REGULAR", "EGGSQUISITE", "BRILLIANT"}[*a.Level]
	case ArtifactSpec_SHELL_STONE:
		return []string{"REGULAR", "EGGSQUISITE", "FLAWLESS"}[*a.Level]
	case ArtifactSpec_LUNAR_STONE:
		return []string{"REGULAR", "EGGSQUISITE", "MEGGNIFICENT"}[*a.Level]
	case ArtifactSpec_PROPHECY_STONE:
		fallthrough
	case ArtifactSpec_SOUL_STONE:
		return []string{"REGULAR", "EGGSQUISITE", "RADIANT"}[*a.Level]
	case ArtifactSpec_QUANTUM_STONE:
		return []string{"REGULAR", "PHASED", "MEGGNIFICENT"}[*a.Level]
	case ArtifactSpec_TERRA_STONE:
		return []string{"REGULAR", "RICH", "EGGCEPTIONAL"}[*a.Level]
	case ArtifactSpec_LIFE_STONE:
		return []string{"REGULAR", "GOOD", "EGGCEPTIONAL"}[*a.Level]
	case ArtifactSpec_CLARITY_STONE:
		return []string{"REGULAR", "EGGSQUISITE", "EGGCEPTIONAL"}[*a.Level]
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
		return "FRAGMENT"
	// Ingredients
	case ArtifactSpec_GOLD_METEORITE:
		return []string{"TINY", "ENRICHED", "SOLID"}[*a.Level]
	case ArtifactSpec_TAU_CETI_GEODE:
		return []string{"TAU", "GLIMMERING", "RADIANT"}[*a.Level]
	case ArtifactSpec_SOLAR_TITANIUM:
		return []string{"ORE", "BAR", "GEOGON"}[*a.Level]
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
		return "?"
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
