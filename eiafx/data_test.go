package eiafx

import "testing"

func TestLoadDataConfig_Populates(t *testing.T) {
	if err := LoadDataConfig(""); err != nil {
		t.Fatalf("LoadDataConfig: %v", err)
	}
	if len(Families) == 0 {
		t.Fatal("Families should not be empty")
	}
	if len(FamilyAFXIds) == 0 {
		t.Fatal("FamilyAFXIds should not be empty")
	}
	if len(CraftingWeights) == 0 {
		t.Fatal("CraftingWeights should not be empty")
	}
}

func TestCraftingWeights_BaseItem(t *testing.T) {
	if err := LoadDataConfig(""); err != nil {
		t.Fatalf("LoadDataConfig: %v", err)
	}
	// Tachyon stone fragment: afx_id=2, afx_level=0. No recipe -> weight 1.0.
	w := CraftingWeights[[2]int{2, 0}]
	if w != 1.0 {
		t.Errorf("tachyon stone fragment weight = %v, want 1.0", w)
	}
}

func TestCraftingWeights_SelfContained(t *testing.T) {
	if err := LoadDataConfig(""); err != nil {
		t.Fatalf("LoadDataConfig: %v", err)
	}
	// Tachyon stone (lowest crafted tier): afx_id=1, afx_level=0. Recipe: 20x fragment (afx_id=2, afx_level=0). Weight = 20.0.
	w := CraftingWeights[[2]int{1, 0}]
	if w != 20.0 {
		t.Errorf("tachyon stone lowest crafted tier weight = %v, want 20.0", w)
	}
}

func TestCraftingWeights_CrossFamily(t *testing.T) {
	if err := LoadDataConfig(""); err != nil {
		t.Fatalf("LoadDataConfig: %v", err)
	}
	// Puzzle Cube T3: afx_id=23, afx_level=2.
	// Recipe: 7x {afx_id=23, afx_level=1} (weight 3) + 2x {afx_id=8, afx_level=0} ornate gusset base (weight 1) = 23.
	w := CraftingWeights[[2]int{23, 2}]
	if w != 23.0 {
		t.Errorf("puzzle cube T3 weight = %v, want 23.0", w)
	}
}

func TestFamilyAFXIds_TachyonStone(t *testing.T) {
	if err := LoadDataConfig(""); err != nil {
		t.Fatalf("LoadDataConfig: %v", err)
	}
	ids, ok := FamilyAFXIds["tachyon-stone"]
	if !ok {
		t.Fatal("tachyon-stone family missing from FamilyAFXIds")
	}
	if len(ids) == 0 {
		t.Fatal("tachyon-stone should have at least one afx_id")
	}
}
