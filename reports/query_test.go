package reports_test

import (
	"strings"
	"testing"

	"github.com/DavidArthurCole/EggLedger/reports"
)

func TestBuildWhereClause_MissionScope(t *testing.T) {
	filters := reports.ReportFilters{
		And: []reports.FilterCondition{
			{TopLevel: "ship", Op: "=", Val: "3"},
			{TopLevel: "duration", Op: "!=", Val: "0"},
		},
	}
	clause, args := reports.BuildWhereClause(filters, "ships")
	if !strings.Contains(clause, "m.ship = ?") {
		t.Errorf("expected ship condition, got: %s", clause)
	}
	if !strings.Contains(clause, "m.duration_type != ?") {
		t.Errorf("expected duration_type condition, got: %s", clause)
	}
	if len(args) != 2 {
		t.Errorf("expected 2 args, got %d", len(args))
	}
}

func TestBuildWhereClause_ArtifactScope(t *testing.T) {
	filters := reports.ReportFilters{
		And: []reports.FilterCondition{
			{TopLevel: "artifact_rarity", Op: ">=", Val: "2"},
			{TopLevel: "artifact_spec_type", Op: "=", Val: "Artifact"},
		},
	}
	clause, args := reports.BuildWhereClause(filters, "artifacts")
	if !strings.Contains(clause, "d.rarity >= ?") {
		t.Errorf("expected rarity condition, got: %s", clause)
	}
	if !strings.Contains(clause, "d.spec_type = ?") {
		t.Errorf("expected spec_type condition, got: %s", clause)
	}
	if len(args) != 2 {
		t.Errorf("expected 2 args, got %d", len(args))
	}
}

func TestBuildWhereClause_BooleanOps(t *testing.T) {
	filters := reports.ReportFilters{
		And: []reports.FilterCondition{
			{TopLevel: "dubcap", Op: "true"},
			{TopLevel: "buggedcap", Op: "false"},
		},
	}
	clause, args := reports.BuildWhereClause(filters, "ships")
	if !strings.Contains(clause, "m.is_dub_cap = 1") {
		t.Errorf("expected is_dub_cap = 1, got: %s", clause)
	}
	if !strings.Contains(clause, "m.is_bugged_cap = 0") {
		t.Errorf("expected is_bugged_cap = 0, got: %s", clause)
	}
	if len(args) != 0 {
		t.Errorf("boolean ops should produce no args, got %d", len(args))
	}
}

func TestGroupByColumn(t *testing.T) {
	cases := map[string]string{
		"ship_type":     "m.ship",
		"duration_type": "m.duration_type",
		"rarity":        "d.rarity",
		"tier":          "d.level",
		"spec_type":     "d.spec_type",
	}
	for groupBy, want := range cases {
		got := reports.GroupByColumn(groupBy)
		if got != want {
			t.Errorf("GroupByColumn(%q) = %q, want %q", groupBy, got, want)
		}
	}
}

func TestConditionToSQL_LaunchDTUsesStrftime(t *testing.T) {
	filters := reports.ReportFilters{
		And: []reports.FilterCondition{
			{TopLevel: "launchDT", Op: ">=", Val: "2025-01-01"},
		},
	}
	clause, args := reports.BuildWhereClause(filters, "ships")
	expected := "m.start_timestamp >= strftime('%s', ?)"
	if !strings.Contains(clause, expected) {
		t.Errorf("expected %q in clause, got: %s", expected, clause)
	}
	if len(args) != 1 || args[0] != "2025-01-01" {
		t.Errorf("expected arg '2025-01-01', got: %v", args)
	}
}

func TestConditionToSQL_ReturnDTLessThan(t *testing.T) {
	filters := reports.ReportFilters{
		And: []reports.FilterCondition{
			{TopLevel: "returnDT", Op: "<", Val: "2025-06-01"},
		},
	}
	clause, args := reports.BuildWhereClause(filters, "ships")
	expected := "m.return_timestamp < strftime('%s', ?)"
	if !strings.Contains(clause, expected) {
		t.Errorf("expected %q in clause, got: %s", expected, clause)
	}
	if len(args) != 1 {
		t.Errorf("expected 1 arg, got: %d", len(args))
	}
}

func TestConditionToSQL_LaunchDTEquals(t *testing.T) {
	filters := reports.ReportFilters{
		And: []reports.FilterCondition{
			{TopLevel: "launchDT", Op: "=", Val: "2025-05-15"},
		},
	}
	clause, args := reports.BuildWhereClause(filters, "ships")
	expected := "m.start_timestamp = strftime('%s', ?)"
	if !strings.Contains(clause, expected) {
		t.Errorf("expected %q in clause, got: %s", expected, clause)
	}
	if len(args) != 1 || args[0] != "2025-05-15" {
		t.Errorf("expected arg '2025-05-15', got: %v", args)
	}
}

func TestBuildTimePivotQuery_MissionDimensions(t *testing.T) {
	def := reports.ReportDefinition{
		Mode:             "time_series",
		GroupBy:          "time_bucket",
		SecondaryGroupBy: "ship_type",
		TimeBucket:       "month",
		AccountId:        "EI1234",
		Filters:          reports.ReportFilters{},
	}
	baseWhere := "m.player_id = ?"
	args := []interface{}{"EI1234"}
	query, outArgs, err := reports.BuildTimePivotQuery(def, baseWhere, args)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if !strings.Contains(query, "strftime('%Y-%m'") {
		t.Errorf("expected month format in query, got: %s", query)
	}
	if !strings.Contains(query, "m.ship") {
		t.Errorf("expected m.ship for ship_type secondary, got: %s", query)
	}
	if strings.Contains(query, "artifact_drops") {
		t.Errorf("mission-only query should not join artifact_drops")
	}
	if !strings.Contains(query, "GROUP BY bucket, grp") {
		t.Errorf("expected GROUP BY bucket, grp, got: %s", query)
	}
	if len(outArgs) < 1 {
		t.Errorf("expected at least 1 arg, got %d", len(outArgs))
	}
}

func TestBuildTimePivotQuery_ArtifactSecondary_JoinsDrops(t *testing.T) {
	def := reports.ReportDefinition{
		Mode:             "time_series",
		GroupBy:          "time_bucket",
		SecondaryGroupBy: "artifact_name",
		TimeBucket:       "month",
		AccountId:        "EI1234",
		Filters:          reports.ReportFilters{},
	}
	baseWhere := "m.player_id = ?"
	args := []interface{}{"EI1234"}
	query, _, err := reports.BuildTimePivotQuery(def, baseWhere, args)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if !strings.Contains(query, "artifact_drops") {
		t.Errorf("artifact secondary should join artifact_drops, got: %s", query)
	}
	if !strings.Contains(query, "d.artifact_id") {
		t.Errorf("expected d.artifact_id for artifact_name secondary, got: %s", query)
	}
}

func TestBuildTimePivotQuery_CustomBucket_AddsWindowCondition(t *testing.T) {
	def := reports.ReportDefinition{
		Mode:             "time_series",
		GroupBy:          "time_bucket",
		SecondaryGroupBy: "duration_type",
		TimeBucket:       "custom",
		CustomBucketN:    3,
		CustomBucketUnit: "month",
		AccountId:        "EI1234",
		Filters:          reports.ReportFilters{},
	}
	baseWhere := "m.player_id = ?"
	args := []interface{}{"EI1234"}
	query, outArgs, err := reports.BuildTimePivotQuery(def, baseWhere, args)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if !strings.Contains(query, "strftime('%s'") {
		t.Errorf("expected window condition with strftime, got: %s", query)
	}
	if len(outArgs) < 2 {
		t.Errorf("expected at least 2 args (player_id + window modifier), got %d", len(outArgs))
	}
}

func TestBuildTimePivotQuery_InvalidSecondary_ReturnsError(t *testing.T) {
	def := reports.ReportDefinition{
		Mode:             "time_series",
		SecondaryGroupBy: "nonexistent_dimension",
		TimeBucket:       "month",
	}
	_, _, err := reports.BuildTimePivotQuery(def, "1=1", nil)
	if err == nil {
		t.Error("expected error for invalid secondary group-by")
	}
}
