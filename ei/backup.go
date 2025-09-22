package ei

import (
	"math"
	"strings"

	"github.com/DavidArthurCole/EggLedger/utils"
	"github.com/pkg/errors"
)

func (fc *EggIncFirstContactResponse) Validate() error {
	if fc.GetErrorCode() > 0 {
		return errors.Errorf("/ei/first_contact: error_code %d", fc.GetErrorCode())
	}
	if fc.Backup == nil || fc.GetBackup().Game == nil {
		return errors.New("backup is empty")
	}
	if fc.GetBackup().Settings == nil {
		return errors.New("backup settings is empty")
	}
	if fc.GetBackup().ArtifactsDb == nil {
		return errors.New("backup has empty artifacts database")
	}
	return nil
}

func (b *Backup) GetEarningsBonus() float64 {
	virtue := b.GetVirtue()
	game := b.GetGame()

	soulEggBonus := 10.0
	prophecyEggBonus := 1.05
	for _, er := range game.GetEpicResearch() {
		if strings.ToLower(er.GetId()) == "soul_eggs" {
			soulEggBonus = float64(er.GetLevel()) + 10
		} else if strings.ToLower(er.GetId()) == "prophecy_bonus" {
			prophecyEggBonus = (float64(er.GetLevel())+5)/100 + 1
		}
	}

	totalPE := float64(game.GetEggsOfProphecy())
	peBonus := math.Pow(float64(prophecyEggBonus), float64(totalPE))

	totalSE := float64(game.GetSoulEggsD())
	seBonus := math.Pow(soulEggBonus, float64(totalSE))

	totalEoTEarned := utils.Sum(virtue.EovEarned, func(v uint32) float64 { return float64(v) })
	eotFactor := math.Pow(1.01, totalEoTEarned)

	return float64(peBonus * seBonus * eotFactor)
}
