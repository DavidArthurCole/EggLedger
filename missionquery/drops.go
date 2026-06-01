package missionquery

import (
	"context"
	"strings"

	"github.com/DavidArthurCole/EggLedger/db"
	"github.com/DavidArthurCole/EggLedger/ei"
	"github.com/DavidArthurCole/EggLedger/eiafx"
	log "github.com/sirupsen/logrus"
)

// GetAllPlayerDrops returns a map of missionId -> drops for every mission
// the player has stored, processing one mission at a time to avoid a large
// transient allocation that can cause GC stop-the-world pauses.
func GetAllPlayerDrops(playerId string) map[string][]MissionDrop {
	result := make(map[string][]MissionDrop)
	err := db.RetrievePlayerCompleteMissionsStream(context.Background(), playerId, func(completeMission *ei.CompleteMissionResponse) error {
		missionId := completeMission.GetInfo().GetIdentifier()
		shipDrops := []MissionDrop{}
		for _, drop := range completeMission.Artifacts {
			spec := drop.GetSpec()
			missionDrop := MissionDrop{
				Id:       int32(spec.GetName()),
				Name:     ei.ArtifactSpec_Name_name[int32(spec.GetName())],
				GameName: spec.CasedName(),
				Level:    int32(spec.GetLevel()),
				Rarity:   int32(spec.GetRarity()),
				Quality:  eiafx.BaseQualityFor(spec),
				IVOrder:  int32(spec.Name.InventoryVisualizerOrder()),
			}
			switch {
			case strings.Contains(missionDrop.Name, "_FRAGMENT"):
				missionDrop.SpecType = "StoneFragment"
			case strings.Contains(missionDrop.Name, "_STONE"):
				missionDrop.SpecType = "Stone"
				missionDrop.EffectString = spec.CombinedEffectString()
			case strings.Contains(missionDrop.Name, "GOLD_METEORITE"),
				strings.Contains(missionDrop.Name, "SOLAR_TITANIUM"),
				strings.Contains(missionDrop.Name, "TAU_CETI_GEODE"):
				missionDrop.SpecType = "Ingredient"
			default:
				missionDrop.SpecType = "Artifact"
				missionDrop.EffectString = spec.CombinedEffectString()
			}
			shipDrops = append(shipDrops, missionDrop)
		}
		result[missionId] = shipDrops
		return nil
	})
	if err != nil {
		log.Error(err)
		return nil
	}
	return result
}

func GetShipDrops(playerId, missionId string) []MissionDrop {
	completeMission, err := db.RetrieveCompleteMission(context.Background(), playerId, missionId)
	if err != nil {
		log.Error(err)
		return nil
	}
	if completeMission == nil {
		// Cache miss for this (player, mission): no drops to report.
		return nil
	}

	shipDrops := []MissionDrop{}
	for _, drop := range completeMission.Artifacts {
		spec := drop.GetSpec()
		missionDrop := MissionDrop{
			Id:       int32(spec.GetName()),
			Name:     ei.ArtifactSpec_Name_name[int32(spec.GetName())],
			GameName: spec.CasedName(),
			Level:    int32(spec.GetLevel()),
			Rarity:   int32(spec.GetRarity()),
			Quality:  eiafx.BaseQualityFor(spec),
			IVOrder:  int32(spec.Name.InventoryVisualizerOrder()),
		}
		switch {
		case strings.Contains(missionDrop.Name, "_FRAGMENT"):
			missionDrop.SpecType = "StoneFragment"
		case strings.Contains(missionDrop.Name, "_STONE"):
			missionDrop.SpecType = "Stone"
			missionDrop.EffectString = spec.CombinedEffectString()
		case strings.Contains(missionDrop.Name, "GOLD_METEORITE"),
			strings.Contains(missionDrop.Name, "SOLAR_TITANIUM"),
			strings.Contains(missionDrop.Name, "TAU_CETI_GEODE"):
			missionDrop.SpecType = "Ingredient"
		default:
			missionDrop.SpecType = "Artifact"
			missionDrop.EffectString = spec.CombinedEffectString()
		}
		shipDrops = append(shipDrops, missionDrop)
	}
	return shipDrops
}
