package eiafx

import (
	"os"
	"testing"

	"github.com/DavidArthurCole/EggLedger/ei"
)

func TestBaseQualityFor(t *testing.T) {
	if err := LoadConfig(os.TempDir()); err != nil {
		t.Fatalf("LoadConfig: %v", err)
	}
	params := Config.GetArtifactParameters()
	if len(params) == 0 {
		t.Fatal("no artifact parameters loaded")
	}

	var sample *ei.ArtifactsConfigurationResponse_ArtifactParameters
	for _, p := range params {
		if p.GetBaseQuality() > 0 {
			sample = p
			break
		}
	}
	if sample == nil {
		t.Skip("no artifact parameter with positive base quality in config")
	}

	// Build a FRESH ArtifactSpec with the same (name, level, rarity). This is
	// exactly the case the old pointer-identity comparison (artifact.Spec == spec)
	// got wrong: a decoded mission drop is a different pointer than the config's
	// spec, so the lookup must match by value.
	s := sample.GetSpec()
	fresh := &ei.ArtifactSpec{Name: s.Name, Level: s.Level, Rarity: s.Rarity}

	if got := BaseQualityFor(fresh); got != sample.GetBaseQuality() {
		t.Fatalf("BaseQualityFor(fresh spec) = %v, want %v", got, sample.GetBaseQuality())
	}

	// An out-of-range spec name (not in the config) returns 0 rather than
	// panicking. (An all-zero spec is NOT unknown: name 0 is a real artifact.)
	badName := ei.ArtifactSpec_Name(99999)
	if q := BaseQualityFor(&ei.ArtifactSpec{Name: &badName}); q != 0 {
		t.Errorf("BaseQualityFor(unknown name) = %v, want 0", q)
	}
}
