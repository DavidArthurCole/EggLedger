package ei_test

import (
	"testing"

	"github.com/DavidArthurCole/EggLedger/ei"
	"github.com/DavidArthurCole/EggLedger/ledgerdata"
)

func setupLedgerDataForMissions(t *testing.T) {
	t.Helper()
	if err := ledgerdata.LoadConfig(t.TempDir()); err != nil {
		t.Fatalf("ledgerdata.LoadConfig: %v", err)
	}
}

// GetDurationString

func TestGetDurationString_Zero(t *testing.T) {
	dur := float64(0)
	m := &ei.MissionInfo{DurationSeconds: &dur}
	if got := m.GetDurationString(); got != "0m" {
		t.Errorf("GetDurationString(0) = %q, want %q", got, "0m")
	}
}

func TestGetDurationString_Seconds(t *testing.T) {
	dur := float64(30)
	m := &ei.MissionInfo{DurationSeconds: &dur}
	if got := m.GetDurationString(); got != "30s" {
		t.Errorf("GetDurationString(30s) = %q, want %q", got, "30s")
	}
}

func TestGetDurationString_Minutes(t *testing.T) {
	dur := float64(90) // 1m30s -> "1m" (truncated)
	m := &ei.MissionInfo{DurationSeconds: &dur}
	if got := m.GetDurationString(); got != "1m" {
		t.Errorf("GetDurationString(90s) = %q, want %q", got, "1m")
	}
}

func TestGetDurationString_HoursAndMinutes(t *testing.T) {
	dur := float64(3*3600 + 45*60) // 3h45m
	m := &ei.MissionInfo{DurationSeconds: &dur}
	if got := m.GetDurationString(); got != "3h45m" {
		t.Errorf("GetDurationString(3h45m) = %q, want %q", got, "3h45m")
	}
}

func TestGetDurationString_DaysHoursMinutes(t *testing.T) {
	dur := float64(2*86400 + 3*3600 + 15*60) // 2d3h15m
	m := &ei.MissionInfo{DurationSeconds: &dur}
	if got := m.GetDurationString(); got != "2d3h15m" {
		t.Errorf("GetDurationString(2d3h15m) = %q, want %q", got, "2d3h15m")
	}
}

// DurationType.Display

func TestDurationTypeDisplay(t *testing.T) {
	tests := []struct {
		d    ei.MissionInfo_DurationType
		want string
	}{
		{ei.MissionInfo_TUTORIAL, "Tutorial"},
		{ei.MissionInfo_SHORT, "Short"},
		{ei.MissionInfo_LONG, "Standard"},
		{ei.MissionInfo_EPIC, "Extended"},
	}
	for _, tc := range tests {
		if got := tc.d.Display(); got != tc.want {
			t.Errorf("DurationType(%v).Display() = %q, want %q", tc.d, got, tc.want)
		}
	}
}

// MissionType.Display

func TestMissionTypeDisplay(t *testing.T) {
	tests := []struct {
		m    ei.MissionInfo_MissionType
		want string
	}{
		{ei.MissionInfo_STANDARD, "Standard"},
		{ei.MissionInfo_VIRTUE, "Virtue"},
	}
	for _, tc := range tests {
		if got := tc.m.Display(); got != tc.want {
			t.Errorf("MissionType(%v).Display() = %q, want %q", tc.m, got, tc.want)
		}
	}
}

// GetCompletedMissions

func TestGetCompletedMissions_Deduplication(t *testing.T) {
	id := "mission-abc"
	status := ei.MissionInfo_COMPLETE
	m1 := &ei.MissionInfo{Identifier: &id, Status: &status}
	m2 := &ei.MissionInfo{Identifier: &id, Status: &status} // duplicate

	fc := &ei.EggIncFirstContactResponse{
		Backup: &ei.Backup{
			ArtifactsDb: &ei.ArtifactsDB{
				MissionArchive: []*ei.MissionInfo{m1, m2},
			},
		},
	}

	missions := fc.GetCompletedMissions()
	if len(missions) != 1 {
		t.Errorf("GetCompletedMissions dedupe: got %d missions, want 1", len(missions))
	}
}

func TestGetCompletedMissions_FiltersNonCompleted(t *testing.T) {
	id1, id2 := "m-complete", "m-exploring"
	complete := ei.MissionInfo_COMPLETE
	exploring := ei.MissionInfo_EXPLORING

	fc := &ei.EggIncFirstContactResponse{
		Backup: &ei.Backup{
			ArtifactsDb: &ei.ArtifactsDB{
				MissionArchive: []*ei.MissionInfo{
					{Identifier: &id1, Status: &complete},
					{Identifier: &id2, Status: &exploring},
				},
			},
		},
	}

	missions := fc.GetCompletedMissions()
	if len(missions) != 1 {
		t.Errorf("GetCompletedMissions filter: got %d missions, want 1", len(missions))
	}
	if missions[0].GetIdentifier() != id1 {
		t.Errorf("GetCompletedMissions filter: got id %q, want %q", missions[0].GetIdentifier(), id1)
	}
}

func TestGetCompletedMissions_SortedByStartTime(t *testing.T) {
	id1, id2 := "m-later", "m-earlier"
	status := ei.MissionInfo_COMPLETE
	t1 := float64(2000)
	t2 := float64(1000)

	fc := &ei.EggIncFirstContactResponse{
		Backup: &ei.Backup{
			ArtifactsDb: &ei.ArtifactsDB{
				MissionArchive: []*ei.MissionInfo{
					{Identifier: &id1, Status: &status, StartTimeDerived: &t1},
					{Identifier: &id2, Status: &status, StartTimeDerived: &t2},
				},
			},
		},
	}

	missions := fc.GetCompletedMissions()
	if len(missions) != 2 {
		t.Fatalf("GetCompletedMissions sort: got %d missions, want 2", len(missions))
	}
	if missions[0].GetIdentifier() != id2 {
		t.Errorf("GetCompletedMissions sort: first mission should be %q (earlier), got %q", id2, missions[0].GetIdentifier())
	}
}

// Spaceship.Name (depends on ledgerdata)

func TestSpaceshipName_KnownShip(t *testing.T) {
	setupLedgerDataForMissions(t)
	if got := ei.MissionInfo_CHICKEN_ONE.Name(); got != "Chicken One" {
		t.Errorf("CHICKEN_ONE.Name() = %q, want %q", got, "Chicken One")
	}
}

func TestSpaceshipName_UnknownShip(t *testing.T) {
	setupLedgerDataForMissions(t)
	// A value not in the shipNames map falls back to the proto string
	unknown := ei.MissionInfo_Spaceship(9999)
	got := unknown.Name()
	if got == "" {
		t.Error("unknown ship Name() should not be empty")
	}
}
