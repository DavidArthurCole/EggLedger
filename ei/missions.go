package ei

import (
	"fmt"
	"sort"
)

func (s MissionInfo_Spaceship) Name() string {
	switch s {
	case MissionInfo_CHICKEN_ONE:
		return "Chicken One"
	case MissionInfo_CHICKEN_NINE:
		return "Chicken Nine"
	case MissionInfo_CHICKEN_HEAVY:
		return "Chicken Heavy"
	case MissionInfo_BCR:
		return "BCR"
	case MissionInfo_MILLENIUM_CHICKEN:
		return "Quintillion Chicken"
	case MissionInfo_CORELLIHEN_CORVETTE:
		return "Cornish-Hen Corvette"
	case MissionInfo_GALEGGTICA:
		return "Galeggtica"
	case MissionInfo_CHICKFIANT:
		return "Defihent"
	case MissionInfo_VOYEGGER:
		return "Voyegger"
	case MissionInfo_HENERPRISE:
		return "Henerprise"
	case MissionInfo_ATREGGIES:
		return "Atreggies Henliner"
	}
	return "Unknown"
}

func (d *MissionInfo) GetDurationString() string {
	seconds := d.GetDurationSeconds()
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

func (d MissionInfo_DurationType) Display() string {
	switch d {
	case MissionInfo_TUTORIAL:
		return "Tutorial"
	case MissionInfo_SHORT:
		return "Short"
	case MissionInfo_LONG:
		return "Standard"
	case MissionInfo_EPIC:
		return "Extended"
	}
	return "Unknown"
}

func (fc *EggIncFirstContactResponse) GetCompletedMissions() []*MissionInfo {
	afxdb := fc.GetBackup().GetArtifactsDb()
	allMissions := append(afxdb.MissionArchive, afxdb.MissionInfos...)
	var completed []*MissionInfo
	// There could be duplicates in the mission archive for whatever stupid
	// reason, even if you don't glitch intentionally. So we need to dedupe.
	seen := make(map[string]struct{})
	for _, mission := range allMissions {
		status := mission.GetStatus()
		if status == MissionInfo_COMPLETE || status == MissionInfo_ARCHIVED {
			id := mission.GetIdentifier()
			if _, exists := seen[id]; !exists {
				completed = append(completed, mission)
				seen[id] = struct{}{}
			}
		}
	}
	sort.SliceStable(completed, func(i, j int) bool {
		return completed[i].GetStartTimeDerived() < completed[j].GetStartTimeDerived()
	})
	return completed
}

func (fc *EggIncFirstContactResponse) GetInProgressMissions() []*MissionInfo {
	var inProgress []*MissionInfo
	for _, mission := range fc.GetBackup().GetArtifactsDb().MissionInfos {
		status := mission.GetStatus()
		if status == MissionInfo_EXPLORING || status == MissionInfo_FUELING || status == MissionInfo_PREPARE_TO_LAUNCH {
			inProgress = append(inProgress, mission)
		}
	}
	sort.SliceStable(inProgress, func(i, j int) bool {
		return inProgress[i].GetStartTimeDerived() < inProgress[j].GetStartTimeDerived()
	})
	return inProgress
}
