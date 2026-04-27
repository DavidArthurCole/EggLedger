package reports

import (
	"testing"
	"time"
)

func TestDateFilterWindowDays_NoFilter(t *testing.T) {
	got := dateFilterWindowDays(ReportFilters{})
	if got != 9999 {
		t.Errorf("want 9999, got %d", got)
	}
}

func TestDateFilterWindowDays_WithLaunchGTE(t *testing.T) {
	ago30 := time.Now().AddDate(0, 0, -30).Format("2006-01-02")
	f := ReportFilters{
		And: []FilterCondition{{TopLevel: "launchDT", Op: ">=", Val: ago30}},
	}
	got := dateFilterWindowDays(f)
	if got < 29 || got > 31 {
		t.Errorf("want ~30, got %d", got)
	}
}

func TestDateFilterWindowDays_PicksMostRestrictive(t *testing.T) {
	ago10 := time.Now().AddDate(0, 0, -10).Format("2006-01-02")
	ago90 := time.Now().AddDate(0, 0, -90).Format("2006-01-02")
	f := ReportFilters{
		And: []FilterCondition{
			{TopLevel: "launchDT", Op: ">=", Val: ago90},
			{TopLevel: "launchDT", Op: ">=", Val: ago10},
		},
	}
	got := dateFilterWindowDays(f)
	if got < 9 || got > 11 {
		t.Errorf("want ~10, got %d", got)
	}
}

func TestClassifyWeight_2D_NoArtifact_IsLow(t *testing.T) {
	def := ReportDefinition{
		Mode:             "aggregate",
		GroupBy:          "ship_type",
		SecondaryGroupBy: "duration_type",
	}
	got := ClassifyWeight(def)
	if got != "LOW" {
		t.Errorf("want LOW, got %s", got)
	}
}

func TestClassifyWeight_2D_ArtifactNoDate_IsHeavy(t *testing.T) {
	def := ReportDefinition{
		Mode:             "aggregate",
		GroupBy:          "ship_type",
		SecondaryGroupBy: "artifact_name",
	}
	got := ClassifyWeight(def)
	if got != "HEAVY" {
		t.Errorf("want HEAVY, got %s", got)
	}
}

func TestClassifyWeight_2D_ArtifactWithDate_IsMedium(t *testing.T) {
	ago30 := time.Now().AddDate(0, 0, -30).Format("2006-01-02")
	def := ReportDefinition{
		Mode:             "aggregate",
		GroupBy:          "ship_type",
		SecondaryGroupBy: "artifact_name",
		Filters: ReportFilters{
			And: []FilterCondition{{TopLevel: "launchDT", Op: ">=", Val: ago30}},
		},
	}
	got := ClassifyWeight(def)
	if got != "MEDIUM" {
		t.Errorf("want MEDIUM, got %s", got)
	}
}
