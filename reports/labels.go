package reports

import (
	"fmt"
	"strconv"

	"github.com/DavidArthurCole/EggLedger/ei"
)

var rarityNames = []string{"Common", "Rare", "Epic", "Legendary"}
var tierNames = []string{"T1", "T2", "T3", "T4"}

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
		return ei.ArtifactSpec_Name(v).CasedName()
	case "artifact_name":
		name := ei.ArtifactSpec_Name(parseInt())
		return name.CasedName()
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
