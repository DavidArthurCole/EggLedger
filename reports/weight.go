package reports

import (
	"strings"
	"time"
)

// ClassifyWeight assigns a heuristic cost tier (LOW, MEDIUM, HEAVY) to a
// report definition so callers can throttle or warn before executing expensive
// queries.
func ClassifyWeight(def ReportDefinition) string {
	// Time-series + secondary group-by: time pivot (most expensive case first)
	if def.SecondaryGroupBy != "" && def.Mode == "time_series" {
		eitherIsArtifact := isArtifactDimension(def.GroupBy) || isArtifactDimension(def.SecondaryGroupBy)
		if eitherIsArtifact {
			if hasDateFilter(def.Filters) {
				return "MEDIUM"
			}
			return "HEAVY"
		}
		if !hasDateFilter(def.Filters) {
			return "HEAVY"
		}
		if dateFilterWindowDays(def.Filters) > 90 {
			return "HEAVY"
		}
		return "MEDIUM"
	}

	// Aggregate 2D pivot
	if def.SecondaryGroupBy != "" {
		eitherIsArtifact := isArtifactDimension(def.GroupBy) || isArtifactDimension(def.SecondaryGroupBy)
		if eitherIsArtifact {
			if hasDateFilter(def.Filters) {
				return "MEDIUM"
			}
			return "HEAVY"
		}
		return "LOW"
	}

	// 1D time series
	if def.Mode == "time_series" {
		if def.TimeBucket == "custom" {
			days := customBucketDays(def.CustomBucketN, def.CustomBucketUnit)
			if days > 90 {
				return "HEAVY"
			}
			return "MEDIUM"
		}
		if !hasDateFilter(def.Filters) {
			return "HEAVY"
		}
		days := dateFilterWindowDays(def.Filters)
		if days > 90 {
			return "HEAVY"
		}
		return "MEDIUM"
	}

	if def.Mode == "aggregate" && hasArtifactScopeFilter(def.Filters) {
		return "MEDIUM"
	}
	return "LOW"
}

func customBucketDays(n int, unit string) int {
	switch unit {
	case "day":
		return n
	case "week":
		return n * 7
	case "month":
		return n * 30
	}
	return n
}

func hasDateFilter(f ReportFilters) bool {
	for _, c := range f.And {
		if c.TopLevel == "launchDT" || c.TopLevel == "returnDT" {
			return true
		}
	}
	for _, group := range f.Or {
		for _, c := range group {
			if c.TopLevel == "launchDT" || c.TopLevel == "returnDT" {
				return true
			}
		}
	}
	return false
}

// dateFilterWindowDays returns a rough estimate of the filter window in days.
// Returns a large sentinel (9999) if the window cannot be determined.
func dateFilterWindowDays(f ReportFilters) int {
	minDays := 9999
	all := make([]FilterCondition, 0, len(f.And))
	all = append(all, f.And...)
	for _, group := range f.Or {
		all = append(all, group...)
	}
	now := time.Now()
	for _, c := range all {
		if c.TopLevel != "launchDT" && c.TopLevel != "returnDT" {
			continue
		}
		if c.Op != ">" && c.Op != ">=" {
			continue
		}
		t, err := time.Parse("2006-01-02", c.Val)
		if err != nil {
			continue
		}
		days := int(now.Sub(t).Hours() / 24)
		if days < minDays {
			minDays = days
		}
	}
	return minDays
}

func hasArtifactScopeFilter(f ReportFilters) bool {
	isArtifactField := func(s string) bool { return strings.HasPrefix(s, "artifact_") }
	for _, c := range f.And {
		if isArtifactField(c.TopLevel) {
			return true
		}
	}
	for _, group := range f.Or {
		for _, c := range group {
			if isArtifactField(c.TopLevel) {
				return true
			}
		}
	}
	return false
}

func isArtifactDimension(groupBy string) bool {
	switch groupBy {
	case "artifact_name", "rarity", "tier", "spec_type":
		return true
	}
	return false
}
