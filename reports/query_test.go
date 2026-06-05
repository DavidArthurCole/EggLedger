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
	clause, args := reports.BuildWhereClause(filters)
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
	clause, args := reports.BuildWhereClause(filters)
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
	clause, args := reports.BuildWhereClause(filters)
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

func TestBuildWhereClause_NumericValidation(t *testing.T) {
	cases := []struct {
		name      string
		cond      reports.FilterCondition
		wantSub   string
		wantArg   any
		wantEmpty bool
	}{
		{
			name:    "valid int tier",
			cond:    reports.FilterCondition{TopLevel: "artifact_tier", Op: "=", Val: "3"},
			wantSub: "d.level = ?",
			wantArg: "3",
		},
		{
			name:    "valid int rarity",
			cond:    reports.FilterCondition{TopLevel: "artifact_rarity", Op: ">=", Val: "2"},
			wantSub: "d.rarity >= ?",
			wantArg: "2",
		},
		{
			name:    "valid int level (mission)",
			cond:    reports.FilterCondition{TopLevel: "level", Op: "=", Val: "5"},
			wantSub: "m.level = ?",
			wantArg: "5",
		},
		{
			name:    "valid int ship (mission)",
			cond:    reports.FilterCondition{TopLevel: "ship", Op: "=", Val: "3"},
			wantSub: "m.ship = ?",
			wantArg: "3",
		},
		{
			name:    "valid int name (artifact_id)",
			cond:    reports.FilterCondition{TopLevel: "artifact_name", Op: "=", Val: "12"},
			wantSub: "d.artifact_id = ?",
			wantArg: "12",
		},
		{
			name:    "valid float quality",
			cond:    reports.FilterCondition{TopLevel: "artifact_quality", Op: ">", Val: "3.5"},
			wantSub: "d.quality > ?",
			wantArg: "3.5",
		},
		{
			name:    "spec_type text stays valid",
			cond:    reports.FilterCondition{TopLevel: "artifact_spec_type", Op: "=", Val: "Artifact"},
			wantSub: "d.spec_type = ?",
			wantArg: "Artifact",
		},
		{
			name:      "non-numeric int field tier yields no clause",
			cond:      reports.FilterCondition{TopLevel: "artifact_tier", Op: "=", Val: "gusset"},
			wantEmpty: true,
		},
		{
			name:      "non-numeric int field rarity yields no clause",
			cond:      reports.FilterCondition{TopLevel: "artifact_rarity", Op: "=", Val: "rare"},
			wantEmpty: true,
		},
		{
			name:      "non-numeric mission ship yields no clause",
			cond:      reports.FilterCondition{TopLevel: "ship", Op: "=", Val: "henliner"},
			wantEmpty: true,
		},
		{
			name:      "non-numeric float quality yields no clause",
			cond:      reports.FilterCondition{TopLevel: "artifact_quality", Op: "=", Val: "abc"},
			wantEmpty: true,
		},
	}
	for _, tc := range cases {
		t.Run(tc.name, func(t *testing.T) {
			filters := reports.ReportFilters{And: []reports.FilterCondition{tc.cond}}
			clause, args := reports.BuildWhereClause(filters)
			if tc.wantEmpty {
				if clause != "" {
					t.Fatalf("expected empty clause, got %q (args %v)", clause, args)
				}
				if len(args) != 0 {
					t.Fatalf("expected no args, got %v", args)
				}
				return
			}
			if !strings.Contains(clause, tc.wantSub) {
				t.Fatalf("expected %q in clause, got %q", tc.wantSub, clause)
			}
			if len(args) != 1 || args[0] != tc.wantArg {
				t.Fatalf("expected arg %v, got %v", tc.wantArg, args)
			}
		})
	}
}

func TestConditionToSQL_LaunchDTUsesStrftime(t *testing.T) {
	filters := reports.ReportFilters{
		And: []reports.FilterCondition{
			{TopLevel: "launchDT", Op: ">=", Val: "2025-01-01"},
		},
	}
	clause, args := reports.BuildWhereClause(filters)
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
	clause, args := reports.BuildWhereClause(filters)
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
	clause, args := reports.BuildWhereClause(filters)
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
	args := []any{"EI1234"}
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
	args := []any{"EI1234"}
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
	args := []any{"EI1234"}
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

func TestFamilyWeightClause_Empty(t *testing.T) {
	clause, args := reports.FamilyWeightClause(nil)
	if clause != "" {
		t.Errorf("empty ids should return empty clause, got: %s", clause)
	}
	if len(args) != 0 {
		t.Errorf("empty ids should return no args, got: %v", args)
	}
}

func TestFamilyWeightClause_SingleId(t *testing.T) {
	clause, args := reports.FamilyWeightClause([]int{1})
	if !strings.Contains(clause, "d.artifact_id IN") {
		t.Errorf("expected IN clause, got: %s", clause)
	}
	if len(args) != 1 || args[0] != 1 {
		t.Errorf("expected [1], got: %v", args)
	}
}

func TestFamilyWeightClause_MultipleIds(t *testing.T) {
	clause, args := reports.FamilyWeightClause([]int{1, 2, 23})
	if !strings.Contains(clause, "?, ?, ?") {
		t.Errorf("expected 3 placeholders, got: %s", clause)
	}
	if len(args) != 3 {
		t.Errorf("expected 3 args, got %d", len(args))
	}
}

func TestBuildWeightedAggregateQuery_IncludesHiddenColumns(t *testing.T) {
	def := reports.ReportDefinition{
		Subject:      "artifacts",
		Mode:         "aggregate",
		GroupBy:      "ship_type",
		FamilyWeight: "tachyon-stone",
	}
	baseWhere := "m.player_id = ?"
	baseArgs := []any{"EI1234"}
	fwClause := "d.artifact_id IN (?, ?)"
	fwArgs := []any{1, 2}
	query, args := reports.BuildWeightedAggregateQuery(def, baseWhere, baseArgs, fwClause, fwArgs)
	if !strings.Contains(query, "d.artifact_id") {
		t.Errorf("expected d.artifact_id in query, got: %s", query)
	}
	if !strings.Contains(query, "d.level") {
		t.Errorf("expected d.level in query, got: %s", query)
	}
	if !strings.Contains(query, "artifact_drops") {
		t.Errorf("expected artifact_drops join, got: %s", query)
	}
	if len(args) != 3 {
		t.Errorf("expected 3 args (1 base + 2 fw), got %d", len(args))
	}
}

func TestBuildWeightedPivotQuery_IncludesHiddenColumns(t *testing.T) {
	def := reports.ReportDefinition{
		Subject:          "artifacts",
		Mode:             "aggregate",
		GroupBy:          "ship_type",
		SecondaryGroupBy: "duration_type",
		FamilyWeight:     "tachyon-stone",
	}
	query, args, err := reports.BuildWeightedPivotQuery(def, "m.player_id = ?", []any{"EI1234"}, "d.artifact_id IN (?)", []any{1})
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if !strings.Contains(query, "d.artifact_id") {
		t.Errorf("expected d.artifact_id in query, got: %s", query)
	}
	if !strings.Contains(query, "d.level") {
		t.Errorf("expected d.level in query, got: %s", query)
	}
	if len(args) != 2 {
		t.Errorf("expected 2 args, got %d", len(args))
	}
}

func TestBuildWeightedTimeSeriesQuery_IncludesHiddenColumns(t *testing.T) {
	def := reports.ReportDefinition{
		Subject:      "artifacts",
		Mode:         "time_series",
		TimeBucket:   "month",
		FamilyWeight: "tachyon-stone",
	}
	query, args := reports.BuildWeightedTimeSeriesQuery(def, "m.player_id = ?", []any{"EI1234"}, "d.artifact_id IN (?)", []any{1})
	if !strings.Contains(query, "d.artifact_id") {
		t.Errorf("expected d.artifact_id in query, got: %s", query)
	}
	if !strings.Contains(query, "d.level") {
		t.Errorf("expected d.level in query, got: %s", query)
	}
	if !strings.Contains(query, "bucket") {
		t.Errorf("expected bucket in query, got: %s", query)
	}
	if len(args) != 2 {
		t.Errorf("expected 2 args (1 base + 1 fw), got %d", len(args))
	}
}

func TestBuildWeightedTimePivotQuery_IncludesHiddenColumns(t *testing.T) {
	def := reports.ReportDefinition{
		Subject:          "artifacts",
		Mode:             "time_series",
		TimeBucket:       "month",
		SecondaryGroupBy: "ship_type",
		FamilyWeight:     "tachyon-stone",
	}
	query, args, err := reports.BuildWeightedTimePivotQuery(def, "m.player_id = ?", []any{"EI1234"}, "d.artifact_id IN (?)", []any{1})
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if !strings.Contains(query, "d.artifact_id") {
		t.Errorf("expected d.artifact_id in query, got: %s", query)
	}
	if !strings.Contains(query, "d.level") {
		t.Errorf("expected d.level in query, got: %s", query)
	}
	if !strings.Contains(query, "grp") {
		t.Errorf("expected grp alias in query, got: %s", query)
	}
	if len(args) != 2 {
		t.Errorf("expected 2 args (1 base + 1 fw), got %d", len(args))
	}
}
