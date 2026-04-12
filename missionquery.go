package main

import (
	"context"
	"strings"

	"github.com/DavidArthurCole/EggLedger/db"
	"github.com/DavidArthurCole/EggLedger/ei"
	log "github.com/sirupsen/logrus"
)

type MissionDrop struct {
	Id           int32   `json:"id"`
	SpecType     string  `json:"specType"`
	Name         string  `json:"name"`
	GameName     string  `json:"gameName"`
	EffectString string  `json:"effectString"`
	Level        int32   `json:"level"`
	Rarity       int32   `json:"rarity"`
	Quality      float64 `json:"quality"`
	IVOrder      int32   `json:"ivOrder"`
}

type PossibleMission struct {
	Ship      *ei.MissionInfo_Spaceship `json:"ship"`
	Durations []*DurationConfig         `json:"durations"`
}

type DurationConfig struct {
	DurationType     *ei.MissionInfo_DurationType `json:"durationType"`
	MinQuality       float64                      `json:"minQuality"`
	MaxQuality       float64                      `json:"maxQuality"`
	LevelQualityBump float64                      `json:"levelQualityBump"`
	MaxLevels        int32                        `json:"maxLevels"`
}

func handleGetMissionIds(playerId string) []string {
	ids, err := db.RetrievePlayerCompleteMissionIds(context.Background(), playerId)
	if err != nil {
		log.Error(err)
		return nil
	}
	return ids
}

func handleGetExistingData() []DatabaseAccount {
	knownAccounts := []DatabaseAccount{}
	for _, knownAccount := range _storage.KnownAccounts {
		ids, err := db.RetrievePlayerCompleteMissionIds(context.Background(), knownAccount.Id)
		if err != nil {
			log.Error(err)
		} else if len(ids) > 0 {
			knownAccounts = append(knownAccounts, DatabaseAccount{
				Id:           knownAccount.Id,
				Nickname:     knownAccount.Nickname,
				MissionCount: len(ids),
				EBString:     knownAccount.EBString,
				AccountColor: knownAccount.AccountColor,
			})
		}
	}
	return knownAccounts
}

func handleViewMissionsOfEid(eid string) []DatabaseMission {
	dbMissions, err := viewMissionsOfId(context.Background(), eid)
	if err != nil {
		log.Error(err)
		return nil
	}
	return dbMissions
}

func handleGetMissionInfo(playerId, missionId string) DatabaseMission {
	return getMissionInformation(context.Background(), playerId, missionId)
}

func handleGetDurationConfigs() []PossibleMission {
	possibleMissions := []PossibleMission{}
	for _, mission := range _eiAfxConfigMissions {
		ship := mission.Ship
		durations := []*DurationConfig{}
		for _, duration := range mission.GetDurations() {
			durationConfig := &DurationConfig{
				DurationType:     duration.DurationType,
				MinQuality:       float64(duration.GetMinQuality()),
				MaxQuality:       float64(duration.GetMaxQuality()),
				LevelQualityBump: float64(duration.GetLevelQualityBump()),
				MaxLevels:        int32(len(mission.LevelMissionRequirements)),
			}
			durations = append(durations, durationConfig)
		}
		possibleMissions = append(possibleMissions, PossibleMission{Ship: ship, Durations: durations})
	}
	return possibleMissions
}

func handleGetShipDrops(playerId, missionId string) []MissionDrop {
	completeMission, err := db.RetrieveCompleteMission(context.Background(), playerId, missionId)
	if err != nil {
		log.Error(err)
		return nil
	}

	shipDrops := []MissionDrop{}
	for _, drop := range completeMission.Artifacts {
		spec := drop.GetSpec()
		var foundQuality float64
		for _, artifact := range _eiAfxConfigArtis {
			if artifact.Spec == spec {
				foundQuality = *artifact.BaseQuality
				break
			}
		}
		missionDrop := MissionDrop{
			Id:       int32(spec.GetName()),
			Name:     ei.ArtifactSpec_Name_name[int32(spec.GetName())],
			GameName: spec.CasedName(),
			Level:    int32(*drop.Spec.Level),
			Rarity:   int32(*drop.Spec.Rarity),
			Quality:  foundQuality,
			IVOrder:  int32(spec.Name.InventoryVisualizerOrder()),
		}
		switch {
		case strings.Contains(missionDrop.Name, "_FRAGMENT"):
			missionDrop.SpecType = "StoneFragment"
		case strings.Contains(missionDrop.Name, "_STONE"):
			missionDrop.SpecType = "Stone"
			missionDrop.EffectString = spec.DropEffectString()
		case strings.Contains(missionDrop.Name, "GOLD_METEORITE"),
			strings.Contains(missionDrop.Name, "SOLAR_TITANIUM"),
			strings.Contains(missionDrop.Name, "TAU_CETI_GEODE"):
			missionDrop.SpecType = "Ingredient"
		default:
			missionDrop.SpecType = "Artifact"
			missionDrop.EffectString = spec.DropEffectString()
		}
		shipDrops = append(shipDrops, missionDrop)
	}
	return shipDrops
}
