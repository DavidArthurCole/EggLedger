package main

import (
	"testing"

	"github.com/DavidArthurCole/EggLedger/ei"
)

// isBuggedCap: mission launched between 1712721600 (2024-04-10 00:00 EST)
// and 1713286800 (2024-04-16 13:00 EST) is bugged.

func makeTimestampMission(startTimeDerived float64) *ei.CompleteMissionResponse {
	return &ei.CompleteMissionResponse{
		Info: &ei.MissionInfo{
			StartTimeDerived: &startTimeDerived,
		},
	}
}

func TestIsBuggedCap_InsideRange(t *testing.T) {
	// 1712900000 is inside the bugged window
	m := makeTimestampMission(1712900000)
	if !isBuggedCap(m) {
		t.Error("expected isBuggedCap = true for timestamp inside bugged window")
	}
}

func TestIsBuggedCap_BeforeRange(t *testing.T) {
	m := makeTimestampMission(1712721599)
	if isBuggedCap(m) {
		t.Error("expected isBuggedCap = false for timestamp before bugged window")
	}
}

func TestIsBuggedCap_AfterRange(t *testing.T) {
	m := makeTimestampMission(1713286801)
	if isBuggedCap(m) {
		t.Error("expected isBuggedCap = false for timestamp after bugged window")
	}
}

func TestIsBuggedCap_AtLowerBound(t *testing.T) {
	// Exactly at lower bound: NOT bugged (> not >=)
	m := makeTimestampMission(1712721600)
	if isBuggedCap(m) {
		t.Error("expected isBuggedCap = false at exact lower bound (exclusive)")
	}
}

func TestIsBuggedCap_AtUpperBound(t *testing.T) {
	// Exactly at upper bound: NOT bugged (< not <=)
	m := makeTimestampMission(1713286800)
	if isBuggedCap(m) {
		t.Error("expected isBuggedCap = false at exact upper bound (exclusive)")
	}
}

// isDubCap: capacity >= 1.7 * nominal => dubcap.
// _nominalShipCapacities must be initialized first.

func TestIsDubCap_Normal(t *testing.T) {
	_nominalShipCapacitiesOnce.Do(initNominalShipCapacities)

	// CHICKEN_ONE SHORT level 0 - look up nominal, set capacity to 1x nominal
	ship := ei.MissionInfo_CHICKEN_ONE
	dur := ei.MissionInfo_SHORT
	level := uint32(0)
	nominal := _nominalShipCapacities[ship][dur][level]
	capacity := uint32(nominal) // 1.0x nominal - not a dubcap

	m := &ei.CompleteMissionResponse{
		Info: &ei.MissionInfo{
			Ship:         &ship,
			DurationType: &dur,
			Level:        &level,
			Capacity:     &capacity,
		},
	}
	if isDubCap(m) {
		t.Error("expected isDubCap = false for 1x nominal capacity")
	}
}

func TestIsDubCap_AboveThreshold(t *testing.T) {
	_nominalShipCapacitiesOnce.Do(initNominalShipCapacities)

	ship := ei.MissionInfo_CHICKEN_ONE
	dur := ei.MissionInfo_SHORT
	level := uint32(0)
	nominal := _nominalShipCapacities[ship][dur][level]
	capacity := uint32(nominal * 2.0) // 2x nominal - definitely a dubcap

	m := &ei.CompleteMissionResponse{
		Info: &ei.MissionInfo{
			Ship:         &ship,
			DurationType: &dur,
			Level:        &level,
			Capacity:     &capacity,
		},
	}
	if !isDubCap(m) {
		t.Error("expected isDubCap = true for 2x nominal capacity")
	}
}

func TestIsDubCap_BelowThreshold(t *testing.T) {
	_nominalShipCapacitiesOnce.Do(initNominalShipCapacities)

	ship := ei.MissionInfo_CHICKEN_ONE
	dur := ei.MissionInfo_SHORT
	level := uint32(0)
	nominal := _nominalShipCapacities[ship][dur][level]
	capacity := uint32(nominal * 1.5) // 1.5x nominal - below 1.7 threshold, not a dubcap

	m := &ei.CompleteMissionResponse{
		Info: &ei.MissionInfo{
			Ship:         &ship,
			DurationType: &dur,
			Level:        &level,
			Capacity:     &capacity,
		},
	}
	if isDubCap(m) {
		t.Error("expected isDubCap = false for 1.5x nominal capacity (below 1.7 threshold)")
	}
}

func TestIsDubCap_AtThreshold(t *testing.T) {
	_nominalShipCapacitiesOnce.Do(initNominalShipCapacities)

	ship := ei.MissionInfo_CHICKEN_ONE
	dur := ei.MissionInfo_SHORT
	level := uint32(0)
	nominal := _nominalShipCapacities[ship][dur][level]
	// Set capacity to 1.8x nominal, which is clearly >= 1.7x threshold
	capacity := uint32(nominal * 1.8)

	m := &ei.CompleteMissionResponse{
		Info: &ei.MissionInfo{
			Ship:         &ship,
			DurationType: &dur,
			Level:        &level,
			Capacity:     &capacity,
		},
	}
	if !isDubCap(m) {
		t.Error("expected isDubCap = true for capacity at 1.8x nominal (clearly above 1.7x threshold)")
	}
}
