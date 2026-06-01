package missionquery

import (
	"context"

	"github.com/DavidArthurCole/EggLedger/db"
	"github.com/DavidArthurCole/EggLedger/ei"
	"github.com/DavidArthurCole/EggLedger/eiafx"
	"github.com/DavidArthurCole/EggLedger/missionpacking"
	"github.com/DavidArthurCole/EggLedger/storage"
	log "github.com/sirupsen/logrus"
)

var _storage *storage.AppStorage

// SetStorage injects the shared application storage pointer. It must be called
// during startup before any binding handler in this package can fire.
func SetStorage(s *storage.AppStorage) { _storage = s }

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

type DatabaseAccount struct {
	Id                  string  `json:"id"`
	Nickname            string  `json:"nickname"`
	MissionCount        int     `json:"missionCount"`
	EBString            string  `json:"ebString"`
	AccountColor        string  `json:"accountColor"`
	LastMissionReturnDT float64 `json:"lastMissionReturnDT"`
}

func GetMissionIds(playerId string) []string {
	ids, err := db.RetrievePlayerCompleteMissionIds(context.Background(), playerId)
	if err != nil {
		log.Error(err)
		return nil
	}
	return ids
}

func GetExistingData() []DatabaseAccount {
	_storage.Lock()
	accounts := make([]storage.Account, len(_storage.KnownAccounts))
	copy(accounts, _storage.KnownAccounts)
	_storage.Unlock()
	knownAccounts := []DatabaseAccount{}
	for _, knownAccount := range accounts {
		count, maxReturnTS, err := db.RetrievePlayerMissionStats(context.Background(), knownAccount.Id)
		if err != nil {
			log.Error(err)
		} else if count > 0 {
			knownAccounts = append(knownAccounts, DatabaseAccount{
				Id:                  knownAccount.Id,
				Nickname:            knownAccount.Nickname,
				MissionCount:        count,
				EBString:            knownAccount.EBString,
				AccountColor:        knownAccount.AccountColor,
				LastMissionReturnDT: maxReturnTS,
			})
		}
	}
	return knownAccounts
}

func ViewMissionsOfEid(eid string) []missionpacking.DatabaseMission {
	dbMissions, err := viewMissionsOfId(context.Background(), eid)
	if err != nil {
		log.Error(err)
		return nil
	}
	return dbMissions
}

func GetMissionInfo(playerId, missionId string) missionpacking.DatabaseMission {
	return missionpacking.GetMissionInformation(context.Background(), playerId, missionId)
}

func GetDurationConfigs() []PossibleMission {
	possibleMissions := []PossibleMission{}
	for _, mission := range eiafx.Config.MissionParameters {
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

func viewMissionsOfId(ctx context.Context, eid string) ([]missionpacking.DatabaseMission, error) {
	// Fast path: if all missions have been backfilled with migration-6 filter
	// columns, build the list from DB columns only (no payload decompression).
	pending, countErr := db.CountPendingFilterCols(ctx, eid)
	if countErr == nil && pending == 0 {
		metas, err := db.RetrievePlayerMissionMeta(ctx, eid)
		if err != nil {
			log.Error(err)
			return nil, err
		}
		missions := make([]missionpacking.DatabaseMission, 0, len(metas))
		for _, meta := range metas {
			missions = append(missions, missionpacking.MissionMetaToDBMission(meta))
		}
		return missions, nil
	}

	// Slow path: decompress and decode every payload (used before backfill runs).
	completeMissions, err := db.RetrievePlayerCompleteMissions(ctx, eid)
	if err != nil {
		log.Error(err)
		return nil, err
	}
	missionArr := []missionpacking.DatabaseMission{}
	for _, completeMission := range completeMissions {
		missionArr = append(missionArr, missionpacking.CompileMissionInformation(completeMission))
	}

	// Kick off a background backfill so the next View call uses the fast path.
	// This is the one-time migration for users who view without fetching first.
	if countErr == nil && pending > 0 {
		go func() {
			if _, err := db.ResolvePendingFilterCols(
				context.Background(), eid, missionpacking.ComputeMissionFilterCols, nil,
			); err != nil {
				log.Errorf("background filter col backfill for %s: %s", eid, err)
			}
		}()
	}

	return missionArr, nil
}
