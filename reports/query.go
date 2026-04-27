package reports

import (
	"fmt"
	"strings"
)

// BuildWhereClause converts ReportFilters into a parameterized SQL fragment
// (without the leading WHERE keyword). Mission-scope conditions reference m.*,
// artifact-scope conditions reference d.*.
func BuildWhereClause(filters ReportFilters, _ string) (string, []interface{}) {
	var clauses []string
	var args []interface{}

	addCond := func(c FilterCondition) {
		clause, arg, hasArg := conditionToSQL(c)
		if clause == "" {
			return
		}
		clauses = append(clauses, clause)
		if hasArg {
			args = append(args, arg)
		}
	}

	for _, c := range filters.And {
		addCond(c)
	}

	for _, group := range filters.Or {
		var orParts []string
		for _, c := range group {
			clause, arg, hasArg := conditionToSQL(c)
			if clause == "" {
				continue
			}
			orParts = append(orParts, clause)
			if hasArg {
				args = append(args, arg)
			}
		}
		if len(orParts) > 0 {
			clauses = append(clauses, "("+strings.Join(orParts, " OR ")+")")
		}
	}

	if len(clauses) == 0 {
		return "", nil
	}
	return strings.Join(clauses, " AND "), args
}

var missionFieldToColumn = map[string]string{
	"ship":     "m.ship",
	"duration": "m.duration_type",
	"level":    "m.level",
	"target":   "m.target",
	"type":     "m.mission_type",
	"launchDT": "m.start_timestamp",
	"returnDT": "m.return_timestamp",
}

var artifactFieldToColumn = map[string]string{
	"artifact_rarity":    "d.rarity",
	"artifact_spec_type": "d.spec_type",
	"artifact_name":      "d.artifact_id",
	"artifact_tier":      "d.level",
	"artifact_quality":   "d.quality",
}

func conditionToSQL(c FilterCondition) (clause string, arg interface{}, hasArg bool) {
	switch c.TopLevel {
	case "dubcap":
		if c.Op == "true" {
			return "m.is_dub_cap = 1", nil, false
		}
		return "m.is_dub_cap = 0", nil, false
	case "buggedcap":
		if c.Op == "true" {
			return "m.is_bugged_cap = 1", nil, false
		}
		return "m.is_bugged_cap = 0", nil, false
	case "drops":
		if c.Op == "c" {
			return "EXISTS (SELECT 1 FROM artifact_drops WHERE mission_id = m.mission_id AND player_id = m.player_id AND artifact_id = ?)", c.Val, true
		}
		if c.Op == "dnc" {
			return "NOT EXISTS (SELECT 1 FROM artifact_drops WHERE mission_id = m.mission_id AND player_id = m.player_id AND artifact_id = ?)", c.Val, true
		}
		return "", nil, false
	}

	if c.TopLevel == "launchDT" || c.TopLevel == "returnDT" {
		col := missionFieldToColumn[c.TopLevel]
		switch c.Op {
		case ">", "<", ">=", "<=":
			return fmt.Sprintf("%s %s strftime('%%s', ?)", col, c.Op), c.Val, true
		case "=", "d=":
			return fmt.Sprintf("%s = strftime('%%s', ?)", col), c.Val, true
		}
		return "", nil, false
	}

	if col, ok := missionFieldToColumn[c.TopLevel]; ok {
		switch c.Op {
		case "=", "!=", ">", "<", ">=", "<=":
			return fmt.Sprintf("%s %s ?", col, c.Op), c.Val, true
		}
		return "", nil, false
	}

	if col, ok := artifactFieldToColumn[c.TopLevel]; ok {
		switch c.Op {
		case "=", "!=", ">", "<", ">=", "<=":
			return fmt.Sprintf("%s %s ?", col, c.Op), c.Val, true
		case "c":
			return fmt.Sprintf("%s LIKE ?", col), "%" + c.Val + "%", true
		case "dnc":
			return fmt.Sprintf("%s NOT LIKE ?", col), "%" + c.Val + "%", true
		}
		return "", nil, false
	}

	return "", nil, false
}

// GroupByColumn maps a groupBy dimension to its SQL column expression.
func GroupByColumn(groupBy string) string {
	switch groupBy {
	case "ship_type":
		return "m.ship"
	case "duration_type":
		return "m.duration_type"
	case "level":
		return "m.level"
	case "mission_type":
		return "m.mission_type"
	case "mission_target":
		return "m.target"
	case "artifact_name":
		return "d.artifact_id"
	case "rarity":
		return "d.rarity"
	case "tier":
		return "d.level"
	case "spec_type":
		return "d.spec_type"
	default:
		return ""
	}
}

// TimeBucketFormat returns the strftime format string for a time bucket type.
func TimeBucketFormat(timeBucket, customUnit string) string {
	switch timeBucket {
	case "day":
		return "%Y-%m-%d"
	case "week":
		return "%Y-%W"
	case "month":
		return "%Y-%m"
	case "year":
		return "%Y"
	case "custom":
		switch customUnit {
		case "week":
			return "%Y-%W"
		case "month":
			return "%Y-%m"
		default:
			return "%Y-%m-%d"
		}
	}
	return "%Y-%m"
}

// CustomWindowCondition returns a SQL condition for the "last N units" window
// used by custom time buckets. Returns "" if not applicable.
func CustomWindowCondition(n int, unit string) (string, interface{}) {
	if n <= 0 {
		return "", nil
	}
	var modifier string
	switch unit {
	case "day":
		modifier = fmt.Sprintf("-%d days", n)
	case "week":
		modifier = fmt.Sprintf("-%d days", n*7)
	case "month":
		modifier = fmt.Sprintf("-%d months", n)
	default:
		return "", nil
	}
	return "m.start_timestamp >= strftime('%s', 'now', ?)", modifier
}

// BuildTimePivotQuery generates a time-bucket x secondary-dimension query
// returning three result columns (bucket, grp, count), grouped by bucket and grp.
// Returns (query, args, error).
func BuildTimePivotQuery(def ReportDefinition, baseWhere string, args []interface{}) (string, []interface{}, error) {
	col2 := GroupByColumn(def.SecondaryGroupBy)
	if col2 == "" {
		return "", nil, fmt.Errorf("unsupported secondary group-by dimension %q", def.SecondaryGroupBy)
	}

	format := TimeBucketFormat(def.TimeBucket, def.CustomBucketUnit)
	bucketExpr := "strftime('" + format + "', datetime(m.start_timestamp, 'unixepoch'))"
	needsArtifactJoin := strings.HasPrefix(col2, "d.") || strings.Contains(baseWhere, "d.")

	out := make([]interface{}, len(args))
	copy(out, args)

	windowWhere := ""
	if def.TimeBucket == "custom" {
		cond, modifier := CustomWindowCondition(def.CustomBucketN, def.CustomBucketUnit)
		if cond != "" {
			windowWhere = " AND " + cond
			out = append(out, modifier)
		}
	}

	var query string
	if needsArtifactJoin {
		query = fmt.Sprintf(`
            SELECT %s AS bucket, CAST(%s AS TEXT) AS grp, COUNT(*) AS count
            FROM artifact_drops d
            JOIN mission m ON d.mission_id = m.mission_id AND d.player_id = m.player_id
            WHERE %s AND d.drop_index >= 0%s
            GROUP BY bucket, grp
            ORDER BY bucket ASC, grp ASC`, bucketExpr, col2, baseWhere, windowWhere)
	} else {
		query = fmt.Sprintf(`
            SELECT %s AS bucket, CAST(%s AS TEXT) AS grp, COUNT(*) AS count
            FROM mission m
            WHERE %s%s
            GROUP BY bucket, grp
            ORDER BY bucket ASC, grp ASC`, bucketExpr, col2, baseWhere, windowWhere)
	}
	return query, out, nil
}
