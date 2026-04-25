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
