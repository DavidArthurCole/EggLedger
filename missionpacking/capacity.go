package missionpacking

import (
	"sync"

	"github.com/DavidArthurCole/EggLedger/ei"
	"github.com/DavidArthurCole/EggLedger/eiafx"
)

// nominalShipCapacities maps ship+duration to the per-level nominal capacity
// slice, derived from eiafx.Config at first use. Guarded by capsOnce.
var nominalShipCapacities = map[ei.MissionInfo_Spaceship]map[ei.MissionInfo_DurationType][]float32{}

var capsOnce sync.Once

// initNominalShipCapacities builds nominalShipCapacities from eiafx.Config.
func initNominalShipCapacities() {
	//Loop through ships, for each duration, get the capacity - generate the capacities for each level with capacity + (cap increase * level)
	for _, mission := range eiafx.Config.MissionParameters {
		durations := mission.GetDurations()
		nominalShipCapacities[mission.GetShip()] = map[ei.MissionInfo_DurationType][]float32{}
		for _, duration := range durations {
			nominalShipCapacities[mission.GetShip()][duration.GetDurationType()] = []float32{}
			if len(mission.GetLevelMissionRequirements()) == 0 {
				nominalShipCapacities[mission.GetShip()][duration.GetDurationType()] = append(nominalShipCapacities[mission.GetShip()][duration.GetDurationType()], float32(duration.GetCapacity()))
			} else {
				for level := 0; level <= len(mission.GetLevelMissionRequirements()); level++ {
					nominalShipCapacities[mission.GetShip()][duration.GetDurationType()] = append(nominalShipCapacities[mission.GetShip()][duration.GetDurationType()], float32(duration.GetCapacity())+(float32(duration.GetLevelCapacityBump())*float32(level)))
				}
			}
		}
	}
}

// ensureCaps lazily initializes the nominal-capacity table exactly once.
func ensureCaps() { capsOnce.Do(initNominalShipCapacities) }

// ShipCapacities returns the nominal capacity slice for a ship+duration, self-initializing the table.
func ShipCapacities(ship ei.MissionInfo_Spaceship, dur ei.MissionInfo_DurationType) ([]float32, bool) {
	ensureCaps()
	caps, ok := nominalShipCapacities[ship][dur]
	return caps, ok
}
