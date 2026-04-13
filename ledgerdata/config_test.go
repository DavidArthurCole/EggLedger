package ledgerdata_test

import (
	"os"
	"path/filepath"
	"testing"
	"time"

	"github.com/DavidArthurCole/EggLedger/ledgerdata"
)

func TestLoadConfig_EmbeddedFallback(t *testing.T) {
	// Pass a non-existent internal dir so the embedded fallback is used.
	tmp := t.TempDir()
	err := ledgerdata.LoadConfig(filepath.Join(tmp, "nonexistent"))
	if err != nil {
		t.Fatalf("LoadConfig returned error: %v", err)
	}
	if len(ledgerdata.Config.ShipNames) == 0 {
		t.Fatal("expected ShipNames to be populated from embedded fallback")
	}
	if len(ledgerdata.Config.FarmerRoles) == 0 {
		t.Fatal("expected FarmerRoles to be populated from embedded fallback")
	}
	if len(ledgerdata.Config.ArtifactEffects) == 0 {
		t.Fatal("expected ArtifactEffects to be populated from embedded fallback")
	}
	if len(ledgerdata.Config.ArtifactTargets) == 0 {
		t.Fatal("expected ArtifactTargets to be populated from embedded fallback")
	}
}

func TestLoadConfig_FreshCacheUsed(t *testing.T) {
	// Write a minimal valid JSON to a temp internal dir.
	tmp := t.TempDir()
	cacheFile := filepath.Join(tmp, "ledger-display-data.json")
	data := `{"artifactEffects":{},"farmerRoles":[{"oom":0,"name":"TestFarmer","color":"000000"}],"shipNames":{},"artifactTargets":[]}`
	if err := os.WriteFile(cacheFile, []byte(data), 0644); err != nil {
		t.Fatal(err)
	}
	// Touch the file to be recent (within 7 days).
	now := time.Now()
	if err := os.Chtimes(cacheFile, now, now); err != nil {
		t.Fatal(err)
	}

	err := ledgerdata.LoadConfig(tmp)
	if err != nil {
		t.Fatalf("LoadConfig returned error: %v", err)
	}
	if len(ledgerdata.Config.FarmerRoles) != 1 || ledgerdata.Config.FarmerRoles[0].Name != "TestFarmer" {
		t.Fatalf("expected fresh cache to be used, got: %+v", ledgerdata.Config.FarmerRoles)
	}
}

func TestLoadConfig_StaleCacheIgnored(t *testing.T) {
	// Write a minimal valid JSON but backdate it by 8 days.
	tmp := t.TempDir()
	cacheFile := filepath.Join(tmp, "ledger-display-data.json")
	data := `{"artifactEffects":{},"farmerRoles":[{"oom":0,"name":"StaleData","color":"000000"}],"shipNames":{},"artifactTargets":[]}`
	if err := os.WriteFile(cacheFile, []byte(data), 0644); err != nil {
		t.Fatal(err)
	}
	stale := time.Now().Add(-8 * 24 * time.Hour)
	if err := os.Chtimes(cacheFile, stale, stale); err != nil {
		t.Fatal(err)
	}

	err := ledgerdata.LoadConfig(tmp)
	if err != nil {
		t.Fatalf("LoadConfig returned error: %v", err)
	}
	// Stale cache should be ignored; embedded fallback has 52 roles.
	if len(ledgerdata.Config.FarmerRoles) < 52 {
		t.Fatalf("expected embedded fallback with 52 roles, got %d", len(ledgerdata.Config.FarmerRoles))
	}
	// Confirm the stale data was not used.
	if ledgerdata.Config.FarmerRoles[0].Name == "StaleData" {
		t.Fatal("stale cache was used instead of embedded fallback")
	}
}

func TestLoadConfig_KnownShipName(t *testing.T) {
	err := ledgerdata.LoadConfig(t.TempDir())
	if err != nil {
		t.Fatal(err)
	}
	name, ok := ledgerdata.Config.ShipNames["CHICKEN_ONE"]
	if !ok {
		t.Fatal("CHICKEN_ONE not in ShipNames")
	}
	if name != "Chicken One" {
		t.Fatalf("expected 'Chicken One', got %q", name)
	}
}

func TestLoadConfig_KnownArtifactEffect(t *testing.T) {
	err := ledgerdata.LoadConfig(t.TempDir())
	if err != nil {
		t.Fatal(err)
	}
	effects, ok := ledgerdata.Config.ArtifactEffects["TACHYON_DEFLECTOR"]
	if !ok {
		t.Fatal("TACHYON_DEFLECTOR not in ArtifactEffects")
	}
	// Level 3 (EPIC), Rarity 3 (LEGENDARY) should be "20%"
	if effects[3][3] != "20%" {
		t.Fatalf("expected '20%%', got %q", effects[3][3])
	}
}

func TestLoadConfig_ArtifactTierNames(t *testing.T) {
	if err := ledgerdata.LoadConfig(t.TempDir()); err != nil {
		t.Fatal(err)
	}
	tiers, ok := ledgerdata.Config.ArtifactTierNames["TACHYON_DEFLECTOR"]
	if !ok {
		t.Fatal("TACHYON_DEFLECTOR not in ArtifactTierNames")
	}
	if len(tiers) != 4 {
		t.Fatalf("expected 4 tiers for TACHYON_DEFLECTOR, got %d", len(tiers))
	}
	if tiers[3] != "EGGCEPTIONAL" {
		t.Fatalf("expected tier[3] = %q, got %q", "EGGCEPTIONAL", tiers[3])
	}
}

func TestLoadConfig_InventoryVisualizerOrder(t *testing.T) {
	if err := ledgerdata.LoadConfig(t.TempDir()); err != nil {
		t.Fatal(err)
	}
	order, ok := ledgerdata.Config.InventoryVisualizerOrder["BOOK_OF_BASAN"]
	if !ok {
		t.Fatal("BOOK_OF_BASAN not in InventoryVisualizerOrder")
	}
	if order != 33 {
		t.Fatalf("expected order 33, got %d", order)
	}
}

func TestLoadConfig_GenericBenefitStrings(t *testing.T) {
	if err := ledgerdata.LoadConfig(t.TempDir()); err != nil {
		t.Fatal(err)
	}
	s, ok := ledgerdata.Config.GenericBenefitStrings["QUANTUM_METRONOME"]
	if !ok {
		t.Fatal("QUANTUM_METRONOME not in GenericBenefitStrings")
	}
	if s != "[+^b] egg laying rate" {
		t.Fatalf("expected %q, got %q", "[+^b] egg laying rate", s)
	}
}

func TestLoadConfig_ArtifactTypes(t *testing.T) {
	if err := ledgerdata.LoadConfig(t.TempDir()); err != nil {
		t.Fatal(err)
	}
	tests := []struct {
		key  string
		want string
	}{
		{"BOOK_OF_BASAN", "ARTIFACT"},
		{"TACHYON_STONE", "STONE"},
		{"TACHYON_STONE_FRAGMENT", "STONE_INGREDIENT"},
		{"GOLD_METEORITE", "INGREDIENT"},
	}
	for _, tc := range tests {
		got, ok := ledgerdata.Config.ArtifactTypes[tc.key]
		if !ok {
			t.Errorf("%s not in ArtifactTypes", tc.key)
			continue
		}
		if got != tc.want {
			t.Errorf("ArtifactTypes[%q] = %q, want %q", tc.key, got, tc.want)
		}
	}
}

func TestLoadConfig_StoneFragmentMap(t *testing.T) {
	if err := ledgerdata.LoadConfig(t.TempDir()); err != nil {
		t.Fatal(err)
	}
	stone, ok := ledgerdata.Config.StoneFragmentMap["TACHYON_STONE_FRAGMENT"]
	if !ok {
		t.Fatal("TACHYON_STONE_FRAGMENT not in StoneFragmentMap")
	}
	if stone != "TACHYON_STONE" {
		t.Fatalf("expected %q, got %q", "TACHYON_STONE", stone)
	}
}
