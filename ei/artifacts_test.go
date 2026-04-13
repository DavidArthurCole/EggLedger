package ei_test

import (
	"testing"

	"github.com/DavidArthurCole/EggLedger/ei"
	"github.com/DavidArthurCole/EggLedger/ledgerdata"
)

func setupLedgerData(t *testing.T) {
	t.Helper()
	if err := ledgerdata.LoadConfig(t.TempDir()); err != nil {
		t.Fatalf("ledgerdata.LoadConfig: %v", err)
	}
}

func TestDropEffectString_KnownArtifact(t *testing.T) {
	setupLedgerData(t)
	// TACHYON_DEFLECTOR level GREATER (3), rarity LEGENDARY (3) should be "20%"
	level := ei.ArtifactSpec_GREATER
	rarity := ei.ArtifactSpec_LEGENDARY
	spec := &ei.ArtifactSpec{
		Name:   ei.ArtifactSpec_TACHYON_DEFLECTOR.Enum(),
		Level:  (*ei.ArtifactSpec_Level)(&level),
		Rarity: &rarity,
	}
	got := spec.DropEffectString()
	if got != "20%" {
		t.Errorf("DropEffectString() = %q, want %q", got, "20%")
	}
}

func TestDropEffectString_UnknownArtifact(t *testing.T) {
	setupLedgerData(t)
	level := ei.ArtifactSpec_INFERIOR
	rarity := ei.ArtifactSpec_COMMON
	spec := &ei.ArtifactSpec{
		Name:   ei.ArtifactSpec_UNKNOWN.Enum(),
		Level:  (*ei.ArtifactSpec_Level)(&level),
		Rarity: &rarity,
	}
	got := spec.DropEffectString()
	if got != "" {
		t.Errorf("DropEffectString() for UNKNOWN = %q, want empty string", got)
	}
}

func TestDropEffectString_NilLevel(t *testing.T) {
	setupLedgerData(t)
	rarity := ei.ArtifactSpec_COMMON
	spec := &ei.ArtifactSpec{
		Name:   ei.ArtifactSpec_TACHYON_DEFLECTOR.Enum(),
		Level:  nil,
		Rarity: &rarity,
	}
	got := spec.DropEffectString()
	if got != "" {
		t.Errorf("DropEffectString() with nil level = %q, want empty string", got)
	}
}

func TestDropEffectString_NilName(t *testing.T) {
	setupLedgerData(t)
	level := ei.ArtifactSpec_INFERIOR
	rarity := ei.ArtifactSpec_COMMON
	spec := &ei.ArtifactSpec{
		Name:   nil,
		Level:  (*ei.ArtifactSpec_Level)(&level),
		Rarity: &rarity,
	}
	got := spec.DropEffectString()
	if got != "" {
		t.Errorf("DropEffectString() with nil name = %q, want empty string", got)
	}
}

func TestTierName_ArtifactT1(t *testing.T) {
	setupLedgerData(t)
	level := ei.ArtifactSpec_INFERIOR
	spec := &ei.ArtifactSpec{
		Name:  ei.ArtifactSpec_TACHYON_DEFLECTOR.Enum(),
		Level: (*ei.ArtifactSpec_Level)(&level),
	}
	if got := spec.TierName(); got != "WEAK" {
		t.Errorf("TierName() = %q, want %q", got, "WEAK")
	}
}

func TestTierName_ArtifactT4(t *testing.T) {
	setupLedgerData(t)
	level := ei.ArtifactSpec_GREATER
	spec := &ei.ArtifactSpec{
		Name:  ei.ArtifactSpec_TACHYON_DEFLECTOR.Enum(),
		Level: (*ei.ArtifactSpec_Level)(&level),
	}
	if got := spec.TierName(); got != "EGGCEPTIONAL" {
		t.Errorf("TierName() = %q, want %q", got, "EGGCEPTIONAL")
	}
}

func TestTierName_StoneFragment(t *testing.T) {
	setupLedgerData(t)
	level := ei.ArtifactSpec_INFERIOR
	spec := &ei.ArtifactSpec{
		Name:  ei.ArtifactSpec_TACHYON_STONE_FRAGMENT.Enum(),
		Level: (*ei.ArtifactSpec_Level)(&level),
	}
	if got := spec.TierName(); got != "FRAGMENT" {
		t.Errorf("TierName() = %q, want %q", got, "FRAGMENT")
	}
}

func TestTierName_Ingredient(t *testing.T) {
	setupLedgerData(t)
	level := ei.ArtifactSpec_NORMAL
	spec := &ei.ArtifactSpec{
		Name:  ei.ArtifactSpec_GOLD_METEORITE.Enum(),
		Level: (*ei.ArtifactSpec_Level)(&level),
	}
	if got := spec.TierName(); got != "SOLID" {
		t.Errorf("TierName() = %q, want %q", got, "SOLID")
	}
}

func TestTierName_UnknownArtifact(t *testing.T) {
	setupLedgerData(t)
	level := ei.ArtifactSpec_INFERIOR
	spec := &ei.ArtifactSpec{
		Name:  ei.ArtifactSpec_UNKNOWN.Enum(),
		Level: (*ei.ArtifactSpec_Level)(&level),
	}
	if got := spec.TierName(); got != "?" {
		t.Errorf("TierName() for UNKNOWN = %q, want %q", got, "?")
	}
}

func TestTierName_NilLevel(t *testing.T) {
	setupLedgerData(t)
	spec := &ei.ArtifactSpec{
		Name: ei.ArtifactSpec_TACHYON_DEFLECTOR.Enum(),
	}
	if got := spec.TierName(); got != "?" {
		t.Errorf("TierName() with nil Level = %q, want %q", got, "?")
	}
}

func TestTierName_NilName(t *testing.T) {
	setupLedgerData(t)
	level := ei.ArtifactSpec_INFERIOR
	spec := &ei.ArtifactSpec{
		Level: (*ei.ArtifactSpec_Level)(&level),
	}
	if got := spec.TierName(); got != "?" {
		t.Errorf("TierName() with nil Name = %q, want %q", got, "?")
	}
}

func TestGenericBenefitString_KnownArtifact(t *testing.T) {
	setupLedgerData(t)
	spec := &ei.ArtifactSpec{Name: ei.ArtifactSpec_QUANTUM_METRONOME.Enum()}
	if got := spec.GenericBenefitString(); got != "[+^b] egg laying rate" {
		t.Errorf("GenericBenefitString() = %q, want %q", got, "[+^b] egg laying rate")
	}
}

func TestGenericBenefitString_Stone(t *testing.T) {
	setupLedgerData(t)
	spec := &ei.ArtifactSpec{Name: ei.ArtifactSpec_SOUL_STONE.Enum()}
	if got := spec.GenericBenefitString(); got != "[+^b] bonus per Soul Egg" {
		t.Errorf("GenericBenefitString() = %q, want %q", got, "[+^b] bonus per Soul Egg")
	}
}

func TestGenericBenefitString_Unknown(t *testing.T) {
	setupLedgerData(t)
	spec := &ei.ArtifactSpec{Name: ei.ArtifactSpec_UNKNOWN.Enum()}
	if got := spec.GenericBenefitString(); got != "" {
		t.Errorf("GenericBenefitString() for UNKNOWN = %q, want empty", got)
	}
}

func TestGenericBenefitString_TungstenAnkhMatchesDemeter(t *testing.T) {
	// TUNGSTEN_ANKH and DEMETERS_NECKLACE share the same benefit string.
	setupLedgerData(t)
	ankh := &ei.ArtifactSpec{Name: ei.ArtifactSpec_TUNGSTEN_ANKH.Enum()}
	necklace := &ei.ArtifactSpec{Name: ei.ArtifactSpec_DEMETERS_NECKLACE.Enum()}
	if ankh.GenericBenefitString() != necklace.GenericBenefitString() {
		t.Errorf("expected TUNGSTEN_ANKH and DEMETERS_NECKLACE to share benefit string, got %q vs %q",
			ankh.GenericBenefitString(), necklace.GenericBenefitString())
	}
}

func TestGenericBenefitString_NilName(t *testing.T) {
	setupLedgerData(t)
	spec := &ei.ArtifactSpec{Name: nil}
	if got := spec.GenericBenefitString(); got != "" {
		t.Errorf("GenericBenefitString() with nil name = %q, want empty string", got)
	}
}

func TestInventoryVisualizerOrder_Artifact(t *testing.T) {
	setupLedgerData(t)
	if got := ei.ArtifactSpec_BOOK_OF_BASAN.InventoryVisualizerOrder(); got != 33 {
		t.Errorf("BOOK_OF_BASAN.InventoryVisualizerOrder() = %d, want 33", got)
	}
}

func TestInventoryVisualizerOrder_Stone(t *testing.T) {
	setupLedgerData(t)
	if got := ei.ArtifactSpec_TACHYON_STONE.InventoryVisualizerOrder(); got != 6 {
		t.Errorf("TACHYON_STONE.InventoryVisualizerOrder() = %d, want 6", got)
	}
}

func TestInventoryVisualizerOrder_StoneFragmentMatchesStone(t *testing.T) {
	// Fragment and stone share the same inventory order.
	setupLedgerData(t)
	stone := ei.ArtifactSpec_TACHYON_STONE.InventoryVisualizerOrder()
	frag := ei.ArtifactSpec_TACHYON_STONE_FRAGMENT.InventoryVisualizerOrder()
	if stone != frag {
		t.Errorf("expected stone and fragment to share order, got %d vs %d", stone, frag)
	}
}

func TestInventoryVisualizerOrder_Unknown(t *testing.T) {
	setupLedgerData(t)
	// Unlisted artifact should return 0.
	if got := ei.ArtifactSpec_UNKNOWN.InventoryVisualizerOrder(); got != 0 {
		t.Errorf("UNKNOWN.InventoryVisualizerOrder() = %d, want 0", got)
	}
}

func TestArtifactType_Artifact(t *testing.T) {
	setupLedgerData(t)
	if got := ei.ArtifactSpec_BOOK_OF_BASAN.ArtifactType(); got != ei.ArtifactSpec_ARTIFACT {
		t.Errorf("BOOK_OF_BASAN.ArtifactType() = %v, want ARTIFACT", got)
	}
}

func TestArtifactType_Stone(t *testing.T) {
	setupLedgerData(t)
	if got := ei.ArtifactSpec_TACHYON_STONE.ArtifactType(); got != ei.ArtifactSpec_STONE {
		t.Errorf("TACHYON_STONE.ArtifactType() = %v, want STONE", got)
	}
}

func TestArtifactType_StoneIngredient(t *testing.T) {
	setupLedgerData(t)
	if got := ei.ArtifactSpec_TACHYON_STONE_FRAGMENT.ArtifactType(); got != ei.ArtifactSpec_STONE_INGREDIENT {
		t.Errorf("TACHYON_STONE_FRAGMENT.ArtifactType() = %v, want STONE_INGREDIENT", got)
	}
}

func TestArtifactType_Ingredient(t *testing.T) {
	setupLedgerData(t)
	if got := ei.ArtifactSpec_GOLD_METEORITE.ArtifactType(); got != ei.ArtifactSpec_INGREDIENT {
		t.Errorf("GOLD_METEORITE.ArtifactType() = %v, want INGREDIENT", got)
	}
}

func TestArtifactType_Unknown(t *testing.T) {
	setupLedgerData(t)
	// UNKNOWN is not in the map; fallback is ARTIFACT.
	if got := ei.ArtifactSpec_UNKNOWN.ArtifactType(); got != ei.ArtifactSpec_ARTIFACT {
		t.Errorf("UNKNOWN.ArtifactType() = %v, want ARTIFACT (fallback)", got)
	}
}

func TestCorrespondingStone(t *testing.T) {
	setupLedgerData(t)
	if got := ei.ArtifactSpec_TACHYON_STONE_FRAGMENT.CorrespondingStone(); got != ei.ArtifactSpec_TACHYON_STONE {
		t.Errorf("TACHYON_STONE_FRAGMENT.CorrespondingStone() = %v, want TACHYON_STONE", got)
	}
}

func TestCorrespondingStone_NonFragment(t *testing.T) {
	setupLedgerData(t)
	// Non-fragments are not in the map; result is UNKNOWN.
	if got := ei.ArtifactSpec_TACHYON_STONE.CorrespondingStone(); got != ei.ArtifactSpec_UNKNOWN {
		t.Errorf("TACHYON_STONE.CorrespondingStone() = %v, want UNKNOWN", got)
	}
}

func TestCorrespondingFragment(t *testing.T) {
	setupLedgerData(t)
	if got := ei.ArtifactSpec_TACHYON_STONE.CorrespondingFragment(); got != ei.ArtifactSpec_TACHYON_STONE_FRAGMENT {
		t.Errorf("TACHYON_STONE.CorrespondingFragment() = %v, want TACHYON_STONE_FRAGMENT", got)
	}
}

func TestCorrespondingFragment_NonStone(t *testing.T) {
	setupLedgerData(t)
	// Non-stones are not values in the map; result is UNKNOWN.
	if got := ei.ArtifactSpec_TACHYON_STONE_FRAGMENT.CorrespondingFragment(); got != ei.ArtifactSpec_UNKNOWN {
		t.Errorf("TACHYON_STONE_FRAGMENT.CorrespondingFragment() = %v, want UNKNOWN", got)
	}
}
