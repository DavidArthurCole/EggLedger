package reports

import (
	"fmt"
	"strconv"

	"github.com/DavidArthurCole/EggLedger/ei"
	"github.com/DavidArthurCole/EggLedger/ledgerdata"
)

var rarityNames = []string{"Common", "Rare", "Epic", "Legendary"}
var tierNames = []string{"T1", "T2", "T3", "T4"}

// artifactDisplayName returns the human-readable display name for an artifact
// enum value by looking it up in ledgerdata.Config.ArtifactTargets. Falls back
// to CasedName() if no match is found.
func artifactDisplayName(v int32) string {
	protoName := ei.ArtifactSpec_Name(v).String()
	for _, t := range ledgerdata.Config.ArtifactTargets {
		if t.Name == protoName {
			return t.DisplayName
		}
	}
	return ei.ArtifactSpec_Name(v).CasedName()
}

// FormatLabel converts a raw SQL GROUP BY value to a display string.
func FormatLabel(groupBy string, rawVal string) string {
	parseInt := func() int {
		v, _ := strconv.Atoi(rawVal)
		return v
	}
	switch groupBy {
	case "ship_type":
		return ei.MissionInfo_Spaceship(parseInt()).Name()
	case "duration_type":
		return ei.MissionInfo_DurationType(parseInt()).Display()
	case "level":
		return fmt.Sprintf("Level %s", rawVal)
	case "mission_type":
		return ei.MissionInfo_MissionType(parseInt()).Display()
	case "mission_target":
		v := parseInt()
		if v < 0 {
			return "None (Pre 1.27)"
		}
		if v == 0 {
			return "Untargeted"
		}
		return artifactDisplayName(int32(v))
	case "artifact_name":
		return artifactDisplayName(int32(parseInt()))
	case "rarity":
		i := parseInt()
		if i >= 0 && i < len(rarityNames) {
			return rarityNames[i]
		}
		return rawVal
	case "tier":
		i := parseInt()
		if i >= 0 && i < len(tierNames) {
			return tierNames[i]
		}
		return rawVal
	case "spec_type":
		return rawVal
	case "time_bucket":
		return rawVal
	}
	return rawVal
}
