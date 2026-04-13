package db

import (
	"context"
	"testing"

	"github.com/DavidArthurCole/EggLedger/ei"
	"google.golang.org/protobuf/proto"
)

// makeTestPayload creates a valid compressed AuthenticatedMessage payload
// wrapping the given CompleteMissionResponse. This matches the exact format
// that InsertCompleteMission stores and ResolvePendingFilterCols decodes.
func makeTestPayload(t *testing.T, resp *ei.CompleteMissionResponse) []byte {
	t.Helper()
	msgBytes, err := proto.Marshal(resp)
	if err != nil {
		t.Fatalf("proto.Marshal CompleteMissionResponse: %v", err)
	}
	authMsg := &ei.AuthenticatedMessage{Message: msgBytes}
	authBytes, err := proto.Marshal(authMsg)
	if err != nil {
		t.Fatalf("proto.Marshal AuthenticatedMessage: %v", err)
	}
	compressed, err := compress(authBytes)
	if err != nil {
		t.Fatalf("compress: %v", err)
	}
	return compressed
}

// insertRawMission inserts a mission row without populating the migration-6
// filter columns, leaving them at their DEFAULT -1 sentinel (pending state),
// as rows inserted before migration 6 would look.
func insertRawMission(t *testing.T, ctx context.Context, playerId, missionId string, startTs float64, payload []byte) {
	t.Helper()
	_, err := _db.ExecContext(ctx,
		`INSERT INTO mission(player_id, mission_id, start_timestamp, complete_payload, mission_type)
		 VALUES (?, ?, ?, ?, 0);`,
		playerId, missionId, startTs, payload)
	if err != nil {
		t.Fatalf("insertRawMission %s: %v", missionId, err)
	}
}

func makeSimpleResponse() *ei.CompleteMissionResponse {
	ship := ei.MissionInfo_CHICKEN_ONE
	dur := ei.MissionInfo_SHORT
	level := uint32(0)
	capacity := uint32(6)
	durSecs := float64(300)
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

// InsertCompleteMission

func TestInsertCompleteMission_StoresAndRetrieves(t *testing.T) {
	setupTestDB(t)
	ctx := context.Background()

	cols := MissionFilterCols{
		Ship:            1,
		DurationType:    0,
		Level:           2,
		Capacity:        8,
		IsDubCap:        false,
		IsBuggedCap:     false,
		Target:          -1,
		ReturnTimestamp: 1000300,
	}
	if err := InsertCompleteMission(ctx, "EI1234", "m1", 1000000, []byte("payload"), 0, cols); err != nil {
		t.Fatalf("InsertCompleteMission: %v", err)
	}

	metas, err := RetrievePlayerMissionMeta(ctx, "EI1234")
	if err != nil {
		t.Fatalf("RetrievePlayerMissionMeta: %v", err)
	}
	if len(metas) != 1 {
		t.Fatalf("expected 1 meta, got %d", len(metas))
	}
	m := metas[0]
	if m.MissionId != "m1" {
		t.Errorf("MissionId: got %q, want %q", m.MissionId, "m1")
	}
	if m.Ship != cols.Ship {
		t.Errorf("Ship: got %d, want %d", m.Ship, cols.Ship)
	}
	if m.DurationType != cols.DurationType {
		t.Errorf("DurationType: got %d, want %d", m.DurationType, cols.DurationType)
	}
	if m.Level != cols.Level {
		t.Errorf("Level: got %d, want %d", m.Level, cols.Level)
	}
	if m.Capacity != cols.Capacity {
		t.Errorf("Capacity: got %d, want %d", m.Capacity, cols.Capacity)
	}
	if m.IsDubCap != cols.IsDubCap {
		t.Errorf("IsDubCap: got %v, want %v", m.IsDubCap, cols.IsDubCap)
	}
	if m.Target != cols.Target {
		t.Errorf("Target: got %d, want %d", m.Target, cols.Target)
	}
	if m.ReturnTimestamp != cols.ReturnTimestamp {
		t.Errorf("ReturnTimestamp: got %v, want %v", m.ReturnTimestamp, cols.ReturnTimestamp)
	}
}

func TestInsertCompleteMission_BoolCols(t *testing.T) {
	setupTestDB(t)
	ctx := context.Background()

	cols := MissionFilterCols{
		Ship: 2, DurationType: 1, Level: 0, Capacity: 12,
		IsDubCap: true, IsBuggedCap: true, Target: 5, ReturnTimestamp: 1000600,
	}
	if err := InsertCompleteMission(ctx, "EI1234", "m1", 1000000, []byte("p"), 0, cols); err != nil {
		t.Fatalf("InsertCompleteMission: %v", err)
	}

	metas, err := RetrievePlayerMissionMeta(ctx, "EI1234")
	if err != nil {
		t.Fatalf("RetrievePlayerMissionMeta: %v", err)
	}
	if len(metas) != 1 {
		t.Fatalf("expected 1 meta, got %d", len(metas))
	}
	m := metas[0]
	if !m.IsDubCap {
		t.Error("IsDubCap: got false, want true")
	}
	if !m.IsBuggedCap {
		t.Error("IsBuggedCap: got false, want true")
	}
	if m.Target != 5 {
		t.Errorf("Target: got %d, want 5", m.Target)
	}
}

// CountPendingFilterCols

func TestCountPendingFilterCols_None(t *testing.T) {
	setupTestDB(t)
	ctx := context.Background()

	count, err := CountPendingFilterCols(ctx, "EI1234")
	if err != nil {
		t.Fatalf("CountPendingFilterCols: %v", err)
	}
	if count != 0 {
		t.Errorf("expected 0 pending, got %d", count)
	}
}

func TestCountPendingFilterCols_WithPending(t *testing.T) {
	setupTestDB(t)
	ctx := context.Background()

	payload := makeTestPayload(t, makeSimpleResponse())
	insertRawMission(t, ctx, "EI1234", "m1", 1000000, payload)
	insertRawMission(t, ctx, "EI1234", "m2", 1001000, payload)

	count, err := CountPendingFilterCols(ctx, "EI1234")
	if err != nil {
		t.Fatalf("CountPendingFilterCols: %v", err)
	}
	if count != 2 {
		t.Errorf("expected 2 pending, got %d", count)
	}
}

func TestCountPendingFilterCols_AllResolved(t *testing.T) {
	setupTestDB(t)
	ctx := context.Background()

	cols := MissionFilterCols{Ship: 1, DurationType: 0, Level: 0, Capacity: 6, Target: -1, ReturnTimestamp: 1000300}
	if err := InsertCompleteMission(ctx, "EI1234", "m1", 1000000, []byte("p"), 0, cols); err != nil {
		t.Fatalf("InsertCompleteMission: %v", err)
	}

	count, err := CountPendingFilterCols(ctx, "EI1234")
	if err != nil {
		t.Fatalf("CountPendingFilterCols: %v", err)
	}
	if count != 0 {
		t.Errorf("expected 0 pending after resolved insert, got %d", count)
	}
}

func TestCountPendingFilterCols_OnlyCountsPlayer(t *testing.T) {
	setupTestDB(t)
	ctx := context.Background()

	payload := makeTestPayload(t, makeSimpleResponse())
	insertRawMission(t, ctx, "EI1234", "m1", 1000000, payload)
	insertRawMission(t, ctx, "EI9999", "m2", 1001000, payload)

	count, err := CountPendingFilterCols(ctx, "EI1234")
	if err != nil {
		t.Fatalf("CountPendingFilterCols: %v", err)
	}
	if count != 1 {
		t.Errorf("expected 1 pending for EI1234 (not EI9999's missions), got %d", count)
	}
}

// RetrievePlayerMissionMeta

func TestRetrievePlayerMissionMeta_Empty(t *testing.T) {
	setupTestDB(t)
	ctx := context.Background()

	metas, err := RetrievePlayerMissionMeta(ctx, "EI1234")
	if err != nil {
		t.Fatalf("RetrievePlayerMissionMeta: %v", err)
	}
	if len(metas) != 0 {
		t.Errorf("expected empty slice, got %d", len(metas))
	}
}

func TestRetrievePlayerMissionMeta_ExcludesPending(t *testing.T) {
	setupTestDB(t)
	ctx := context.Background()

	payload := makeTestPayload(t, makeSimpleResponse())
	insertRawMission(t, ctx, "EI1234", "pending", 1000000, payload)
	cols := MissionFilterCols{Ship: 1, DurationType: 0, Level: 0, Capacity: 6, Target: -1, ReturnTimestamp: 1001300}
	if err := InsertCompleteMission(ctx, "EI1234", "resolved", 1001000, []byte("p"), 0, cols); err != nil {
		t.Fatalf("InsertCompleteMission: %v", err)
	}

	metas, err := RetrievePlayerMissionMeta(ctx, "EI1234")
	if err != nil {
		t.Fatalf("RetrievePlayerMissionMeta: %v", err)
	}
	if len(metas) != 1 {
		t.Fatalf("expected 1 meta (resolved only), got %d", len(metas))
	}
	if metas[0].MissionId != "resolved" {
		t.Errorf("expected mission %q, got %q", "resolved", metas[0].MissionId)
	}
}

func TestRetrievePlayerMissionMeta_OrderedByStartTimestamp(t *testing.T) {
	setupTestDB(t)
	ctx := context.Background()

	insertions := []struct {
		id string
		ts float64
	}{
		{"m0", 3000},
		{"m1", 1000},
		{"m2", 2000},
	}
	for _, ins := range insertions {
		cols := MissionFilterCols{Ship: 1, DurationType: 0, Level: 0, Capacity: 6, Target: -1, ReturnTimestamp: ins.ts + 300}
		if err := InsertCompleteMission(ctx, "EI1234", ins.id, ins.ts, []byte("p"), 0, cols); err != nil {
			t.Fatalf("InsertCompleteMission %s: %v", ins.id, err)
		}
	}

	metas, err := RetrievePlayerMissionMeta(ctx, "EI1234")
	if err != nil {
		t.Fatalf("RetrievePlayerMissionMeta: %v", err)
	}
	if len(metas) != 3 {
		t.Fatalf("expected 3 metas, got %d", len(metas))
	}
	for i, want := range []float64{1000, 2000, 3000} {
		if metas[i].StartTimestamp != want {
			t.Errorf("metas[%d].StartTimestamp: got %v, want %v", i, metas[i].StartTimestamp, want)
		}
	}
}

// ResolvePendingFilterCols

func TestResolvePendingFilterCols_NoPending(t *testing.T) {
	setupTestDB(t)
	ctx := context.Background()

	resolved, err := ResolvePendingFilterCols(ctx, "EI1234",
		func(_ float64, _ *ei.CompleteMissionResponse) (MissionFilterCols, bool) {
			return MissionFilterCols{Ship: 1}, true
		}, nil)
	if err != nil {
		t.Fatalf("ResolvePendingFilterCols: %v", err)
	}
	if resolved != 0 {
		t.Errorf("expected 0 resolved, got %d", resolved)
	}
}

func TestResolvePendingFilterCols_UpdatesMissions(t *testing.T) {
	setupTestDB(t)
	ctx := context.Background()

	payload := makeTestPayload(t, makeSimpleResponse())
	insertRawMission(t, ctx, "EI1234", "m1", 1000000, payload)
	insertRawMission(t, ctx, "EI1234", "m2", 1001000, payload)

	wantCols := MissionFilterCols{
		Ship: 3, DurationType: 1, Level: 2, Capacity: 10,
		IsDubCap: true, IsBuggedCap: false, Target: 7, ReturnTimestamp: 1001600,
	}
	resolved, err := ResolvePendingFilterCols(ctx, "EI1234",
		func(_ float64, _ *ei.CompleteMissionResponse) (MissionFilterCols, bool) {
			return wantCols, true
		}, nil)
	if err != nil {
		t.Fatalf("ResolvePendingFilterCols: %v", err)
	}
	if resolved != 2 {
		t.Errorf("expected 2 resolved, got %d", resolved)
	}

	count, err := CountPendingFilterCols(ctx, "EI1234")
	if err != nil {
		t.Fatalf("CountPendingFilterCols after resolve: %v", err)
	}
	if count != 0 {
		t.Errorf("expected 0 pending after resolve, got %d", count)
	}

	metas, err := RetrievePlayerMissionMeta(ctx, "EI1234")
	if err != nil {
		t.Fatalf("RetrievePlayerMissionMeta after resolve: %v", err)
	}
	if len(metas) != 2 {
		t.Fatalf("expected 2 metas, got %d", len(metas))
	}
	for _, m := range metas {
		if m.Ship != wantCols.Ship {
			t.Errorf("Ship: got %d, want %d", m.Ship, wantCols.Ship)
		}
		if m.Capacity != wantCols.Capacity {
			t.Errorf("Capacity: got %d, want %d", m.Capacity, wantCols.Capacity)
		}
		if !m.IsDubCap {
			t.Error("IsDubCap: got false, want true")
		}
		if m.Target != wantCols.Target {
			t.Errorf("Target: got %d, want %d", m.Target, wantCols.Target)
		}
	}
}

func TestResolvePendingFilterCols_Progress(t *testing.T) {
	setupTestDB(t)
	ctx := context.Background()

	payload := makeTestPayload(t, makeSimpleResponse())
	for i, ts := range []float64{1000, 2000, 3000} {
		insertRawMission(t, ctx, "EI1234", []string{"m0", "m1", "m2"}[i], ts, payload)
	}

	var calls []int
	_, err := ResolvePendingFilterCols(ctx, "EI1234",
		func(_ float64, _ *ei.CompleteMissionResponse) (MissionFilterCols, bool) {
			return MissionFilterCols{Ship: 1}, true
		},
		func(done, total int) {
			if total != 3 {
				t.Errorf("progress total: got %d, want 3", total)
			}
			calls = append(calls, done)
		})
	if err != nil {
		t.Fatalf("ResolvePendingFilterCols: %v", err)
	}
	if len(calls) != 3 {
		t.Errorf("expected 3 progress calls, got %d", len(calls))
	}
	if calls[len(calls)-1] != 3 {
		t.Errorf("last progress done value: got %d, want 3", calls[len(calls)-1])
	}
}

func TestResolvePendingFilterCols_OnlyAffectsPlayer(t *testing.T) {
	setupTestDB(t)
	ctx := context.Background()

	payload := makeTestPayload(t, makeSimpleResponse())
	insertRawMission(t, ctx, "EI1234", "m1", 1000000, payload)
	insertRawMission(t, ctx, "EI9999", "m2", 1001000, payload)

	resolved, err := ResolvePendingFilterCols(ctx, "EI1234",
		func(_ float64, _ *ei.CompleteMissionResponse) (MissionFilterCols, bool) {
			return MissionFilterCols{Ship: 2}, true
		}, nil)
	if err != nil {
		t.Fatalf("ResolvePendingFilterCols: %v", err)
	}
	if resolved != 1 {
		t.Errorf("expected 1 resolved for EI1234, got %d", resolved)
	}

	count, err := CountPendingFilterCols(ctx, "EI9999")
	if err != nil {
		t.Fatalf("CountPendingFilterCols EI9999: %v", err)
	}
	if count != 1 {
		t.Errorf("expected EI9999 mission to remain pending, got %d", count)
	}
}

func TestResolvePendingFilterCols_ComputeFnSkips(t *testing.T) {
	setupTestDB(t)
	ctx := context.Background()

	payload := makeTestPayload(t, makeSimpleResponse())
	insertRawMission(t, ctx, "EI1234", "m1", 1000000, payload)

	resolved, err := ResolvePendingFilterCols(ctx, "EI1234",
		func(_ float64, _ *ei.CompleteMissionResponse) (MissionFilterCols, bool) {
			return MissionFilterCols{}, false
		}, nil)
	if err != nil {
		t.Fatalf("ResolvePendingFilterCols: %v", err)
	}
	if resolved != 0 {
		t.Errorf("expected 0 resolved when computeFn returns false, got %d", resolved)
	}

	count, err := CountPendingFilterCols(ctx, "EI1234")
	if err != nil {
		t.Fatalf("CountPendingFilterCols: %v", err)
	}
	if count != 1 {
		t.Errorf("mission should remain pending, got count=%d", count)
	}
}
