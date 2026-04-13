package main

import (
	"context"
	"fmt"
	"time"

	"github.com/DavidArthurCole/EggLedger/db"
	"github.com/DavidArthurCole/EggLedger/ei"
	log "github.com/sirupsen/logrus"
)

type DatabaseMission struct {
	LaunchDT         int64                        `json:"launchDT"` //Unix timestamp
	ReturnDT         int64                        `json:"returnDT"` //Unix timestamp
	MissiondId       string                       `json:"missionId"`
	Ship             *ei.MissionInfo_Spaceship    `json:"ship"`
	ShipString       string                       `json:"shipString"`
	DurationType     *ei.MissionInfo_DurationType `json:"durationType"`
	DurationString   string                       `json:"durationString"`
	Level            int32                        `json:"level"`
	Capacity         int32                        `json:"capacity"`
	NominalCapcity   int32                        `json:"nominalCapacity"`
	IsDubCap         bool                         `json:"isDubCap"`
	IsBuggedCap      bool                         `json:"isBuggedCap"`
	Target           string                       `json:"target"`
	TargetInt        int32                        `json:"targetInt"`
	MissionType      int32                        `json:"missionType"`
	MissionTypeString string                      `json:"missionTypeString"`
}

// durationStringFromSecs converts a float64 duration in seconds to a compact
// human-readable string ("0m", "Xd Yh Zm", etc.). Mirrors MissionInfo.GetDurationString.
func durationStringFromSecs(seconds float64) string {
	switch {
	case seconds == 0:
		return "0m"
	case seconds < 60:
		return fmt.Sprintf("%ds", int(seconds))
	case seconds < 3600:
		return fmt.Sprintf("%dm", int(seconds/60))
	case seconds < 86400:
		return fmt.Sprintf("%dh%dm", int(seconds/3600), int(seconds/60)%60)
	default:
		return fmt.Sprintf("%dd%dh%dm", int(seconds/86400), int(seconds/3600)%24, int(seconds/60)%60)
	}
}

// computeMissionFilterCols extracts the migration-6 filter column values from a
// decoded CompleteMissionResponse. Requires _nominalShipCapacities to be
// initialized before calling (call _nominalShipCapacitiesOnce.Do first).
func computeMissionFilterCols(startTimestamp float64, resp *ei.CompleteMissionResponse) (db.MissionFilterCols, bool) {
	info := resp.GetInfo()
	if info == nil {
		return db.MissionFilterCols{}, false
	}
	ship := info.GetShip()
	durationType := info.GetDurationType()
	level := int32(info.GetLevel())
	capacity := int32(info.GetCapacity())
	durationSeconds := info.GetDurationSeconds()
	returnTimestamp := startTimestamp + durationSeconds

	var target int32 = -1
	if info.TargetArtifact != nil {
		target = int32(info.GetTargetArtifact())
	}

	var nominalCap float32
	if caps, ok := _nominalShipCapacities[ship][durationType]; ok && int(level) < len(caps) {
		nominalCap = caps[int(level)]
	}
	isDubCap := nominalCap > 0 && float32(capacity) >= nominalCap*1.7
	isBuggedCap := startTimestamp > 1712721600 && startTimestamp < 1713286800

	return db.MissionFilterCols{
		Ship:            int32(ship),
		DurationType:    int32(durationType),
		Level:           level,
		Capacity:        capacity,
		IsDubCap:        isDubCap,
		IsBuggedCap:     isBuggedCap,
		Target:          target,
		ReturnTimestamp: returnTimestamp,
	}, true
}

// missionMetaToDBMission builds a DatabaseMission from a lightweight MissionMeta
// row (no payload decompression needed). Requires _nominalShipCapacities to be
// initialized before calling.
func missionMetaToDBMission(meta db.MissionMeta) DatabaseMission {
	ship := ei.MissionInfo_Spaceship(meta.Ship)
	durationType := ei.MissionInfo_DurationType(meta.DurationType)

	var nominalCap float32
	if caps, ok := _nominalShipCapacities[ship][durationType]; ok && int(meta.Level) < len(caps) {
		nominalCap = caps[int(meta.Level)]
	}

	target := ""
	var targetInt int32 = -1
	if meta.Target >= 0 {
		targetName := ei.ArtifactSpec_Name(meta.Target)
		target = properTargetName(&targetName)
		targetInt = meta.Target
	}

	missionType := ei.MissionInfo_MissionType(meta.MissionType)
	durationSecs := meta.ReturnTimestamp - meta.StartTimestamp

	return DatabaseMission{
		LaunchDT:          int64(meta.StartTimestamp),
		ReturnDT:          int64(meta.ReturnTimestamp),
		MissiondId:        meta.MissionId,
		Ship:              &ship,
		ShipString:        ship.Name(),
		DurationType:      &durationType,
		DurationString:    durationStringFromSecs(durationSecs),
		Level:             meta.Level,
		Capacity:          meta.Capacity,
		NominalCapcity:    int32(nominalCap),
		IsDubCap:          meta.IsDubCap,
		IsBuggedCap:       meta.IsBuggedCap,
		Target:            target,
		TargetInt:         targetInt,
		MissionType:       meta.MissionType,
		MissionTypeString: missionType.Display(),
	}
}

func getMissionInformation(ctx context.Context, playerId string, missionId string) DatabaseMission {
	//Get the mission from the database
	completeMission, err := db.RetrieveCompleteMission(ctx, playerId, missionId)
	if err != nil {
		log.Error(err)
		return DatabaseMission{}
	}

	return compileMissionInformation(completeMission)
}

func compileMissionInformation(completeMissionResponse *ei.CompleteMissionResponse) DatabaseMission {
	info := completeMissionResponse.Info
	launchDateTimeObject := time.Unix(int64(*info.StartTimeDerived), 0)
	returnTimeObject := launchDateTimeObject.Add(time.Duration(*info.DurationSeconds * float64(time.Second)))

	missionInst := DatabaseMission{
		LaunchDT:          int64(*info.StartTimeDerived),
		ReturnDT:          returnTimeObject.Unix(),
		DurationString:    info.GetDurationString(),
		MissiondId:        *info.Identifier,
		Ship:              info.Ship,
		ShipString:        info.Ship.Name(),
		DurationType:      info.DurationType,
		Level:             int32(info.GetLevel()),
		Capacity:          int32(info.GetCapacity()),
		NominalCapcity:    int32(_nominalShipCapacities[info.GetShip()][info.GetDurationType()][info.GetLevel()]),
		IsDubCap:          isDubCap(completeMissionResponse),
		IsBuggedCap:       isBuggedCap(completeMissionResponse),
		Target:            properTargetName(info.TargetArtifact),
		MissionType:       int32(info.GetType()),
		MissionTypeString: info.GetType().Display(),
	}
	if missionInst.Target == "" {
		missionInst.TargetInt = -1
	} else {
		missionInst.TargetInt = int32(info.GetTargetArtifact())
	}

	return missionInst
}

func isDubCap(mission *ei.CompleteMissionResponse) bool {
	nominalCapcity := _nominalShipCapacities[mission.Info.GetShip()][mission.Info.GetDurationType()][mission.Info.GetLevel()]
	if float32(mission.Info.GetCapacity()) >= (nominalCapcity * 1.7) { //1.7 to account for rounding errors (1.5x is the max with ER)
		return true
	} else {
		return false
	}
}

func isBuggedCap(mission *ei.CompleteMissionResponse) bool {
	//If it was launched between 2024-04-10 00:00 EST (1712721600 ) and 2024-04-16 13:00 EST (1713286800), it's bugged
	return mission.Info.GetStartTimeDerived() > 1712721600 && mission.Info.GetStartTimeDerived() < 1713286800
}
