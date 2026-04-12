package ei_test

import (
	"testing"

	"github.com/DavidArthurCole/EggLedger/ei"
)

// Sum

func TestSum_Ints(t *testing.T) {
	got := ei.Sum([]int{1, 2, 3, 4}, func(v int) float64 { return float64(v) })
	if got != 10.0 {
		t.Errorf("Sum = %f, want 10.0", got)
	}
}

func TestSum_Empty(t *testing.T) {
	got := ei.Sum([]int{}, func(v int) float64 { return float64(v) })
	if got != 0.0 {
		t.Errorf("Sum of empty = %f, want 0.0", got)
	}
}

// Validate

func TestValidate_ErrorCode(t *testing.T) {
	code := uint32(1)
	fc := &ei.EggIncFirstContactResponse{ErrorCode: &code}
	if err := fc.Validate(); err == nil {
		t.Error("expected error for non-zero error_code")
	}
}

func TestValidate_NilBackup(t *testing.T) {
	code := uint32(0)
	fc := &ei.EggIncFirstContactResponse{ErrorCode: &code}
	if err := fc.Validate(); err == nil {
		t.Error("expected error for nil backup")
	}
}

func TestValidate_Valid(t *testing.T) {
	code := uint32(0)
	soulEggsLevel := uint32(140)
	fc := &ei.EggIncFirstContactResponse{
		ErrorCode: &code,
		Backup: &ei.Backup{
			Game: &ei.Backup_Game{
				EpicResearch: []*ei.Backup_ResearchItem{
					{Id: strPtr("soul_eggs"), Level: &soulEggsLevel},
				},
			},
			Settings:    &ei.Backup_Settings{},
			ArtifactsDb: &ei.ArtifactsDB{},
		},
	}
	if err := fc.Validate(); err != nil {
		t.Errorf("unexpected error for valid response: %v", err)
	}
}

func strPtr(s string) *string { return &s }

// GetEarningsBonus

func TestGetEarningsBonus_BaseCase(t *testing.T) {
	// 0 SE, 0 PE, 0 TE, no epic research -> result = 0
	soulEggsD := float64(0)
	eggsOfProphecy := uint64(0)
	b := &ei.Backup{
		Game: &ei.Backup_Game{
			SoulEggsD:      &soulEggsD,
			EggsOfProphecy: &eggsOfProphecy,
		},
		Virtue: &ei.Backup_Virtue{},
	}
	got := b.GetEarningsBonus()
	if got != 0.0 {
		t.Errorf("GetEarningsBonus (base) = %f, want 0.0", got)
	}
}

func TestGetEarningsBonus_SoulEggsOnly(t *testing.T) {
	// 1000 SE, soulEggBonus=10 (default), 0 PE, 0 TE -> 1.0 * 10*1000 * 1.0 = 10000
	soulEggsD := float64(1000)
	eggsOfProphecy := uint64(0)
	b := &ei.Backup{
		Game: &ei.Backup_Game{
			SoulEggsD:      &soulEggsD,
			EggsOfProphecy: &eggsOfProphecy,
		},
		Virtue: &ei.Backup_Virtue{},
	}
	got := b.GetEarningsBonus()
	if got != 10000.0 {
		t.Errorf("GetEarningsBonus (1000 SE) = %f, want 10000.0", got)
	}
}

func TestGetEarningsBonus_WithEpicResearch(t *testing.T) {
	// soul_eggs level 140 -> soulEggBonus = 140 + 10 = 150
	// 1000 SE, 0 PE, 0 TE -> 1.0 * 150*1000 * 1.0 = 150000
	soulEggsD := float64(1000)
	eggsOfProphecy := uint64(0)
	soulEggsLevel := uint32(140)
	b := &ei.Backup{
		Game: &ei.Backup_Game{
			SoulEggsD:      &soulEggsD,
			EggsOfProphecy: &eggsOfProphecy,
			EpicResearch: []*ei.Backup_ResearchItem{
				{Id: strPtr("soul_eggs"), Level: &soulEggsLevel},
			},
		},
		Virtue: &ei.Backup_Virtue{},
	}
	got := b.GetEarningsBonus()
	if got != 150000.0 {
		t.Errorf("GetEarningsBonus (soul_eggs=140) = %f, want 150000.0", got)
	}
}

func TestGetEarningsBonus_ProphecyEggs(t *testing.T) {
	// soul_eggs=140 (bonus=150), prophecy_bonus=0 (bonus=1.05 default)
	// 1000 SE, 10 PE, 0 TE -> 1.05^10 * 150*1000 * 1.0
	soulEggsD := float64(1000)
	eggsOfProphecy := uint64(10)
	soulEggsLevel := uint32(140)
	b := &ei.Backup{
		Game: &ei.Backup_Game{
			SoulEggsD:      &soulEggsD,
			EggsOfProphecy: &eggsOfProphecy,
			EpicResearch: []*ei.Backup_ResearchItem{
				{Id: strPtr("soul_eggs"), Level: &soulEggsLevel},
			},
		},
		Virtue: &ei.Backup_Virtue{},
	}
	got := b.GetEarningsBonus()
	// 1.05^10 = 1.6288946... -> 150000 * 1.6288946 = 244334.2...
	if got < 244000 || got > 245000 {
		t.Errorf("GetEarningsBonus (10 PE) = %f, want ~244334", got)
	}
}

func TestGetEarningsBonus_WithTE(t *testing.T) {
	// 1000 SE, soul_eggs=140 (bonus=150), 0 PE, TE=100 -> 150000 * 1.01^100
	// 1.01^100 = 2.7048... -> 150000 * 2.7048 = 405723...
	soulEggsD := float64(1000)
	eggsOfProphecy := uint64(0)
	soulEggsLevel := uint32(140)
	b := &ei.Backup{
		Game: &ei.Backup_Game{
			SoulEggsD:      &soulEggsD,
			EggsOfProphecy: &eggsOfProphecy,
			EpicResearch: []*ei.Backup_ResearchItem{
				{Id: strPtr("soul_eggs"), Level: &soulEggsLevel},
			},
		},
		Virtue: &ei.Backup_Virtue{EovEarned: []uint32{100}},
	}
	got := b.GetEarningsBonus()
	// 1.01^100 ≈ 2.7048 -> 405723...
	if got < 405000 || got > 406000 {
		t.Errorf("GetEarningsBonus (TE=100) = %f, want ~405723", got)
	}
}
