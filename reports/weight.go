package reports

import "strings"

// ClassifyWeight assigns a heuristic cost tier (LOW, MEDIUM, HEAVY) to a
// report definition so callers can throttle or warn before executing expensive
// queries.
func ClassifyWeight(def ReportDefinition) string {
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
func dateFilterWindowDays(_ ReportFilters) int {
	return 9999
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
