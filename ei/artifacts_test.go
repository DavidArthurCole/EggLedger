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
