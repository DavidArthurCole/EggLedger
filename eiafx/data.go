package eiafx

import (
	_ "embed"
	"encoding/json"
	"path/filepath"

	"github.com/pkg/errors"
	log "github.com/sirupsen/logrus"
)

//go:embed eiafx-data-min.json
var _eiafxDataJSON []byte

// CraftingWeights maps [afx_id, afx_level] to the T1-equivalent base item weight.
// Base items (no recipe) have weight 1.0. Others: Σ ingredient.Count × ingredient weight,
// expanded recursively including cross-family ingredients.
var CraftingWeights map[[2]int]float64

// FamilyAFXIds maps family ID to all afx_id integers in that family (from child_afx_ids).
var FamilyAFXIds map[string][]int

// Families is the ordered list of families for the UI dropdown.
var Families []FamilyMeta

// FamilyMeta is a summary of an artifact family for the UI.
type FamilyMeta struct {
	ID   string `json:"id"`
	Name string `json:"name"`
}

type artifactDataConfig struct {
	Families []familyData `json:"families"`
}

type familyData struct {
	ID          string     `json:"id"`
	Name        string     `json:"name"`
	ChildAFXIds []int      `json:"child_afx_ids"`
	Tiers       []tierData `json:"tiers"`
}

type tierData struct {
	AFXId    int              `json:"afx_id"`
	AFXLevel int              `json:"afx_level"`
	Recipe   []ingredientData `json:"recipe"`
}

type ingredientData struct {
	AFXId    int `json:"afx_id"`
	AFXLevel int `json:"afx_level"`
	Count    int `json:"count"`
}

// LoadDataConfig loads eiafx data into CraftingWeights, FamilyAFXIds, and Families.
// Priority:
//  1. internal/eiafx-data.json if present and <7 days old
//  2. Embedded eiafx-data-min.json (fallback, always succeeds)
//
// If the cached file is stale or absent, a background goroutine attempts to
// download a fresh copy for the next cold start. Startup is never blocked.
func LoadDataConfig(internalDir string) error {
	if internalDir != "" {
		const cacheFilename = "eiafx-data.json"
		cacheFile := filepath.Join(internalDir, cacheFilename)

		if data, ok := readIfFresh(cacheFile); ok {
			if err := parseDataConfig(data); err == nil {
				return nil
			}
			log.Warnf("eiafx: cached data file is corrupt, will re-download: %v", cacheFile)
			go tryDownloadDataFresh(cacheFile)
		}

		if err := parseDataConfig(_eiafxDataJSON); err != nil {
			return errors.Wrap(err, "eiafx.LoadDataConfig")
		}
		go tryDownloadDataFresh(cacheFile)
		return nil
	}

	// No internalDir: use embedded JSON only, no caching or background download.
	if err := parseDataConfig(_eiafxDataJSON); err != nil {
		return errors.Wrap(err, "eiafx.LoadDataConfig")
	}
	return nil
}

func parseDataConfig(data []byte) error {
	var cfg artifactDataConfig
	if err := json.Unmarshal(data, &cfg); err != nil {
		return err
	}

	// Flat map from (afx_id, afx_level) -> *tierData for weight traversal.
	tierMap := make(map[[2]int]*tierData)
	for i := range cfg.Families {
		for j := range cfg.Families[i].Tiers {
			t := &cfg.Families[i].Tiers[j]
			tierMap[[2]int{t.AFXId, t.AFXLevel}] = t
		}
	}

	// Memoized recursive weight computation.
	memo := make(map[[2]int]float64)
	var computeWeight func(afxId, afxLevel int) float64
	computeWeight = func(afxId, afxLevel int) float64 {
		key := [2]int{afxId, afxLevel}
		if w, ok := memo[key]; ok {
			return w
		}
		t := tierMap[key]
		if t == nil || len(t.Recipe) == 0 {
			memo[key] = 1.0
			return 1.0
		}
		memo[key] = 0 // cycle sentinel: prevents infinite recursion if data has a cycle
		w := 0.0
		for _, ing := range t.Recipe {
			w += float64(ing.Count) * computeWeight(ing.AFXId, ing.AFXLevel)
		}
		memo[key] = w
		return w
	}
	for key := range tierMap {
		computeWeight(key[0], key[1])
	}
	CraftingWeights = memo

	// FamilyAFXIds from child_afx_ids.
	fids := make(map[string][]int, len(cfg.Families))
	for _, f := range cfg.Families {
		fids[f.ID] = f.ChildAFXIds
	}
	FamilyAFXIds = fids

	// Ordered family list for UI.
	fams := make([]FamilyMeta, len(cfg.Families))
	for i, f := range cfg.Families {
		fams[i] = FamilyMeta{ID: f.ID, Name: f.Name}
	}
	Families = fams

	return nil
}
