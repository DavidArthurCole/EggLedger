package main

import (
	"testing"

	"github.com/DavidArthurCole/EggLedger/db"
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

// durationStringFromSecs

func TestDurationStringFromSecs(t *testing.T) {
	cases := []struct {
		secs float64
		want string
	}{
		{0, "0m"},
		{30, "30s"},
		{90, "1m"},
		{3*3600 + 30*60, "3h30m"},
		{2*86400 + 6*3600 + 45*60, "2d6h45m"},
	}
	for _, tc := range cases {
		got := durationStringFromSecs(tc.secs)
		if got != tc.want {
			t.Errorf("durationStringFromSecs(%v) = %q, want %q", tc.secs, got, tc.want)
		}
	}
}

// computeMissionFilterCols

func makeFullMissionResponse(ship ei.MissionInfo_Spaceship, dur ei.MissionInfo_DurationType, level, capacity uint32, durSecs, startTs float64) *ei.CompleteMissionResponse {
	success := true
	return &ei.CompleteMissionResponse{
		Success: &success,
		Info: &ei.MissionInfo{
			Ship:            &ship,
			DurationType:    &dur,
			Level:           &level,
			Capacity:        &capacity,
			DurationSeconds: &durSecs,
		},
	}
}

func TestComputeMissionFilterCols_NilInfo(t *testing.T) {
	resp := &ei.CompleteMissionResponse{}
	_, ok := computeMissionFilterCols(1000000, resp)
	if ok {
		t.Error("expected ok=false when Info is nil")
	}
}

func TestComputeMissionFilterCols_Normal(t *testing.T) {
	_nominalShipCapacitiesOnce.Do(initNominalShipCapacities)

	ship := ei.MissionInfo_CHICKEN_ONE
	dur := ei.MissionInfo_SHORT
	level := uint32(0)
	nominal := _nominalShipCapacities[ship][dur][level]
	capacity := uint32(nominal) // 1x nominal - not a dubcap
	startTs := float64(1000000)

	resp := makeFullMissionResponse(ship, dur, level, capacity, 300, startTs)
	cols, ok := computeMissionFilterCols(startTs, resp)
	if !ok {
		t.Fatal("expected ok=true")
	}
	if cols.Ship != int32(ship) {
		t.Errorf("Ship: got %d, want %d", cols.Ship, int32(ship))
	}
	if cols.DurationType != int32(dur) {
		t.Errorf("DurationType: got %d, want %d", cols.DurationType, int32(dur))
	}
	if cols.Level != int32(level) {
		t.Errorf("Level: got %d, want %d", cols.Level, int32(level))
	}
	if cols.Capacity != int32(capacity) {
		t.Errorf("Capacity: got %d, want %d", cols.Capacity, int32(capacity))
	}
	if cols.IsDubCap {
		t.Error("IsDubCap: got true, want false for 1x nominal")
	}
	if cols.IsBuggedCap {
		t.Error("IsBuggedCap: got true, want false for normal timestamp")
	}
	if cols.Target != -1 {
		t.Errorf("Target: got %d, want -1 (no target)", cols.Target)
	}
	if cols.ReturnTimestamp != startTs+300 {
		t.Errorf("ReturnTimestamp: got %v, want %v", cols.ReturnTimestamp, startTs+300)
	}
}

func TestComputeMissionFilterCols_DubCap(t *testing.T) {
	_nominalShipCapacitiesOnce.Do(initNominalShipCapacities)

	ship := ei.MissionInfo_CHICKEN_ONE
	dur := ei.MissionInfo_SHORT
	level := uint32(0)
	nominal := _nominalShipCapacities[ship][dur][level]
	capacity := uint32(nominal * 2.0) // 2x nominal - clearly a dubcap

	resp := makeFullMissionResponse(ship, dur, level, capacity, 300, 1000000)
	cols, ok := computeMissionFilterCols(1000000, resp)
	if !ok {
		t.Fatal("expected ok=true")
	}
	if !cols.IsDubCap {
		t.Error("IsDubCap: got false, want true for 2x nominal capacity")
	}
}

func TestComputeMissionFilterCols_BuggedCap(t *testing.T) {
	_nominalShipCapacitiesOnce.Do(initNominalShipCapacities)

	ship := ei.MissionInfo_CHICKEN_ONE
	dur := ei.MissionInfo_SHORT
	level := uint32(0)
	capacity := uint32(6)
	// 1712900000 is inside the bugged window (2024-04-10 to 2024-04-16)
	startTs := float64(1712900000)

	resp := makeFullMissionResponse(ship, dur, level, capacity, 300, startTs)
	cols, ok := computeMissionFilterCols(startTs, resp)
	if !ok {
		t.Fatal("expected ok=true")
	}
	if !cols.IsBuggedCap {
		t.Error("IsBuggedCap: got false, want true for timestamp inside bugged window")
	}
}

func TestComputeMissionFilterCols_WithTarget(t *testing.T) {
	_nominalShipCapacitiesOnce.Do(initNominalShipCapacities)

	ship := ei.MissionInfo_CHICKEN_ONE
	dur := ei.MissionInfo_SHORT
	level := uint32(0)
	capacity := uint32(6)
	targetArtifact := ei.ArtifactSpec_BOOK_OF_BASAN

	resp := makeFullMissionResponse(ship, dur, level, capacity, 300, 1000000)
	resp.Info.TargetArtifact = &targetArtifact

	cols, ok := computeMissionFilterCols(1000000, resp)
	if !ok {
		t.Fatal("expected ok=true")
	}
	if cols.Target != int32(targetArtifact) {
		t.Errorf("Target: got %d, want %d", cols.Target, int32(targetArtifact))
	}
}

// missionMetaToDBMission

func TestMissionMetaToDBMission(t *testing.T) {
	_nominalShipCapacitiesOnce.Do(initNominalShipCapacities)
	setupLedgerDataForUtils(t)

	meta := db.MissionMeta{
		MissionId:       "m1",
		StartTimestamp:  1000000,
		ReturnTimestamp: 1000300, // 5-minute duration
		Ship:            int32(ei.MissionInfo_CHICKEN_ONE),
		DurationType:    int32(ei.MissionInfo_SHORT),
		Level:           0,
		Capacity:        6,
		IsDubCap:        false,
		IsBuggedCap:     false,
		Target:          -1,
		MissionType:     int32(ei.MissionInfo_STANDARD),
	}

	dm := missionMetaToDBMission(meta)

	if dm.LaunchDT != 1000000 {
		t.Errorf("LaunchDT: got %d, want 1000000", dm.LaunchDT)
	}
	if dm.ReturnDT != 1000300 {
		t.Errorf("ReturnDT: got %d, want 1000300", dm.ReturnDT)
	}
	if dm.DurationString != "5m" {
		t.Errorf("DurationString: got %q, want %q", dm.DurationString, "5m")
	}
	if dm.MissiondId != "m1" {
		t.Errorf("MissiondId: got %q, want %q", dm.MissiondId, "m1")
	}
	if dm.Level != 0 {
		t.Errorf("Level: got %d, want 0", dm.Level)
	}
	if dm.Capacity != 6 {
		t.Errorf("Capacity: got %d, want 6", dm.Capacity)
	}
	if dm.IsDubCap {
		t.Error("IsDubCap: got true, want false")
	}
	if dm.TargetInt != -1 {
		t.Errorf("TargetInt: got %d, want -1", dm.TargetInt)
	}
	if dm.Target != "" {
		t.Errorf("Target: got %q, want empty string", dm.Target)
	}
	if dm.MissionTypeString != "Home" {
		t.Errorf("MissionTypeString: got %q, want %q", dm.MissionTypeString, "Home")
	}
	if dm.ShipString == "" {
		t.Error("ShipString: got empty string, want non-empty")
	}
	if dm.Ship == nil {
		t.Error("Ship: got nil, want non-nil pointer")
	}
}

func TestMissionMetaToDBMission_WithTarget(t *testing.T) {
	_nominalShipCapacitiesOnce.Do(initNominalShipCapacities)
	setupLedgerDataForUtils(t)

	target := int32(ei.ArtifactSpec_BOOK_OF_BASAN)
	meta := db.MissionMeta{
		MissionId:       "m2",
		StartTimestamp:  1000000,
		ReturnTimestamp: 1000300,
		Ship:            int32(ei.MissionInfo_CHICKEN_ONE),
		DurationType:    int32(ei.MissionInfo_SHORT),
		Level:           0,
		Capacity:        6,
		Target:          target,
		MissionType:     int32(ei.MissionInfo_STANDARD),
	}

	dm := missionMetaToDBMission(meta)

	if dm.TargetInt != target {
		t.Errorf("TargetInt: got %d, want %d", dm.TargetInt, target)
	}
	if dm.Target == "" {
		t.Error("Target: got empty string, want non-empty for known artifact")
	}
}

func TestMissionMetaToDBMission_VirtueMissionType(t *testing.T) {
	_nominalShipCapacitiesOnce.Do(initNominalShipCapacities)
	setupLedgerDataForUtils(t)

	meta := db.MissionMeta{
		MissionId:       "m3",
		StartTimestamp:  1000000,
		ReturnTimestamp: 1000300,
		Ship:            int32(ei.MissionInfo_CHICKEN_ONE),
		DurationType:    int32(ei.MissionInfo_SHORT),
		Level:           0,
		Capacity:        6,
		Target:          -1,
		MissionType:     int32(ei.MissionInfo_VIRTUE),
	}

	dm := missionMetaToDBMission(meta)

	if dm.MissionTypeString != "Virtue" {
		t.Errorf("MissionTypeString: got %q, want %q", dm.MissionTypeString, "Virtue")
	}
}
