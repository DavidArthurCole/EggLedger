package eiafx

import (
	"sync"

	"github.com/DavidArthurCole/EggLedger/ei"
)

// artifactSpecKey identifies an artifact by its (name, level, rarity) tuple,
// which is what determines its configured base quality.
type artifactSpecKey struct {
	name   ei.ArtifactSpec_Name
	level  ei.ArtifactSpec_Level
	rarity ei.ArtifactSpec_Rarity
}

var (
	baseQualityMap  map[artifactSpecKey]float64
	baseQualityOnce sync.Once
)

func buildBaseQualityMap() {
	params := Config.GetArtifactParameters()
	m := make(map[artifactSpecKey]float64, len(params))
	for _, art := range params {
		s := art.GetSpec()
		m[artifactSpecKey{s.GetName(), s.GetLevel(), s.GetRarity()}] = art.GetBaseQuality()
	}
	baseQualityMap = m
}

// BaseQualityFor returns the configured base quality for an artifact spec,
// matched by name+level+rarity. Returns 0 if the spec is not in the config.
// Backed by a lazily-built lookup map, so it is O(1) per call and safe for
// concurrent use. Replaces the previous O(n) linear scan over
// Config.ArtifactParameters (and a pointer-identity comparison bug that made
// the scan never match).
func BaseQualityFor(spec *ei.ArtifactSpec) float64 {
	baseQualityOnce.Do(buildBaseQualityMap)
	return baseQualityMap[artifactSpecKey{spec.GetName(), spec.GetLevel(), spec.GetRarity()}]
}
