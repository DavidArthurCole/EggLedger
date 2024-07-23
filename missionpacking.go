package main

import (
	"context"
	"time"

	"github.com/DavidArthurCole/EggLedger/db"
	"github.com/DavidArthurCole/EggLedger/ei"
	log "github.com/sirupsen/logrus"
)

type DatabaseMission struct {
	LaunchDT       int64                        `json:"launchDT"` //Unix timestamp
	ReturnDT       int64                        `json:"returnDT"` //Unix timestamp
	MissiondId     string                       `json:"missionId"`
	Ship           *ei.MissionInfo_Spaceship    `json:"ship"`
	ShipString     string                       `json:"shipString"`
	DurationType   *ei.MissionInfo_DurationType `json:"durationType"`
	DurationString string                       `json:"durationString"`
	Level          int32                        `json:"level"`
	Capacity       int32                        `json:"capacity"`
	NominalCapcity int32                        `json:"nominalCapacity"`
	IsDubCap       bool                         `json:"isDubCap"`
	IsBuggedCap    bool                         `json:"isBuggedCap"`
	Target         string                       `json:"target"`
	TargetInt      int32                        `json:"targetInt"`
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
		LaunchDT:       int64(*info.StartTimeDerived),
		ReturnDT:       returnTimeObject.Unix(),
		DurationString: info.GetDurationString(),

		MissiondId:     *info.Identifier,
		Ship:           info.Ship,
		ShipString:     info.Ship.Name(),
		DurationType:   info.DurationType,
		Level:          int32(info.GetLevel()),
		Capacity:       int32(info.GetCapacity()),
		NominalCapcity: int32(_nominalShipCapacities[info.GetShip()][info.GetDurationType()][info.GetLevel()]),
		IsDubCap:       isDubCap(completeMissionResponse),
		IsBuggedCap:    isBuggedCap(completeMissionResponse),
		Target:         properTargetName(info.TargetArtifact),
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
