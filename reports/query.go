package reports

import (
	"fmt"
	"strconv"
	"strings"
)

// isInt reports whether s parses as a base-10 integer.
func isInt(s string) bool { _, err := strconv.Atoi(s); return err == nil }

// isFloat reports whether s parses as a 64-bit float.
func isFloat(s string) bool { _, err := strconv.ParseFloat(s, 64); return err == nil }

// BuildWhereClause converts ReportFilters into a parameterized SQL fragment
// (without the leading WHERE keyword). Mission-scope conditions reference m.*,
// artifact-scope conditions reference d.*.
func BuildWhereClause(filters ReportFilters) (string, []any) {
	var clauses []string
	var args []any

	addCond := func(c FilterCondition) {
		clause, cargs := conditionToSQL(c)
		if clause == "" {
			return
		}
		clauses = append(clauses, clause)
		args = append(args, cargs...)
	}

	for _, c := range filters.And {
		addCond(c)
	}

	for _, group := range filters.Or {
		var orParts []string
		for _, c := range group {
			clause, cargs := conditionToSQL(c)
			if clause == "" {
				continue
			}
			orParts = append(orParts, clause)
			args = append(args, cargs...)
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

func conditionToSQL(c FilterCondition) (clause string, args []any) {
	switch c.TopLevel {
	case "dubcap":
		if c.Op == "true" {
			return "m.is_dub_cap = 1", nil
		}
		return "m.is_dub_cap = 0", nil
	case "buggedcap":
		if c.Op == "true" {
			return "m.is_bugged_cap = 1", nil
		}
		return "m.is_bugged_cap = 0", nil
	case "drops":
		if c.Op != "c" && c.Op != "dnc" {
			return "", nil
		}
		if c.Val == "" {
			return "", nil
		}
		parts := strings.Split(c.Val, "_")
		// composite is name_level_rarity_quality; quality (index 3) is ignored.
		cols := []string{"artifact_id", "level", "rarity"}
		var preds []string
		var qargs []any
		for i, col := range cols {
			if i >= len(parts) || parts[i] == "%" || parts[i] == "" {
				continue
			}
			preds = append(preds, "AND "+col+" = ?")
			qargs = append(qargs, parts[i])
		}
		inner := "SELECT 1 FROM artifact_drops WHERE mission_id = m.mission_id AND player_id = m.player_id"
		for _, p := range preds {
			inner += " " + p
		}
		exists := "EXISTS (" + inner + ")"
		if c.Op == "dnc" {
			exists = "NOT " + exists
		}
		return exists, qargs
	}

	if c.TopLevel == "launchDT" || c.TopLevel == "returnDT" {
		col := missionFieldToColumn[c.TopLevel]
		switch c.Op {
		case ">", "<", ">=", "<=":
			return fmt.Sprintf("%s %s strftime('%%s', ?)", col, c.Op), []any{c.Val}
		case "=", "d=":
			return fmt.Sprintf("%s = strftime('%%s', ?)", col), []any{c.Val}
		}
		return "", nil
	}

	if col, ok := missionFieldToColumn[c.TopLevel]; ok {
		switch c.Op {
		case "=", "!=", ">", "<", ">=", "<=":
			// Every remaining missionFieldToColumn field (ship, duration, level,
			// target, type) is an integer column; date fields were handled above.
			// Reject non-numeric values so a malformed value becomes a no-op rather
			// than a silent zero-match like "m.level = 'gusset'".
			if !isInt(c.Val) {
				return "", nil
			}
			return fmt.Sprintf("%s %s ?", col, c.Op), []any{c.Val}
		}
		return "", nil
	}

	if col, ok := artifactFieldToColumn[c.TopLevel]; ok {
		// artifact_spec_type is a TEXT column storing the enum's String() form
		// ("Artifact", "Stone", "StoneFragment", "Ingredient"), so its filter
		// value is genuinely text and must NOT be numerically validated. The other
		// artifact fields are numeric: artifact_quality is a REAL (float), the rest
		// (rarity, name/artifact_id, tier/level) are integers.
		switch c.Op {
		case "=", "!=", ">", "<", ">=", "<=":
			switch c.TopLevel {
			case "artifact_spec_type":
				// text column, no numeric validation
			case "artifact_quality":
				if !isFloat(c.Val) {
					return "", nil
				}
			default:
				if !isInt(c.Val) {
					return "", nil
				}
			}
			return fmt.Sprintf("%s %s ?", col, c.Op), []any{c.Val}
		}
		// Artifact columns only take comparison ops; the c/dnc ("contains") ops are
		// exclusive to the "drops" field, which is handled in the switch above.
		return "", nil
	}

	return "", nil
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
func CustomWindowCondition(n int, unit string) (string, any) {
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

// queryBuilder assembles the SELECT/FROM/WHERE/GROUP BY/ORDER BY skeleton shared
// by every report query. It exists to stop copy-pasting the FROM/JOIN decision and
// the "AND d.drop_index >= 0" suffix across the eight builders. It is intentionally
// dumb: callers pre-format every fragment (select columns, dimensions, order
// expressions) so build() only concatenates. The generated string is byte-identical
// to the historical hand-written queries (see query_golden_test.go).
type queryBuilder struct {
	// indent is the leading whitespace applied to each clause line. Plain builders
	// use 12 spaces; weighted builders use 8.
	indent string
	// selectCols is the comma-joined select list (already formatted, no trailing
	// newline). For weighted queries it may itself contain embedded newlines.
	selectCols string
	// artifactSrc selects the FROM source: true -> artifact_drops d JOIN mission m;
	// false -> mission m. When true, "AND d.drop_index >= 0" is appended to WHERE.
	artifactSrc bool
	// where is the base WHERE fragment without the leading WHERE keyword.
	where string
	// extraWhere holds additional conditions (e.g. the custom time window) appended
	// after the drop_index suffix, each already without a leading "AND ".
	extraWhere []string
	// groupBy and orderBy are the comma-joined GROUP BY / ORDER BY expression lists.
	groupBy string
	orderBy string
}

// build concatenates the query in the canonical clause order, reproducing the
// exact whitespace of the original hand-written builders.
func (q queryBuilder) build() string {
	in := q.indent

	from := "mission m"
	if q.artifactSrc {
		from = "artifact_drops d\n" + in + "JOIN mission m ON d.mission_id = m.mission_id AND d.player_id = m.player_id"
	}

	where := q.where
	if q.artifactSrc {
		where += " AND d.drop_index >= 0"
	}
	for _, c := range q.extraWhere {
		where += " AND " + c
	}

	return "\n" + in + "SELECT " + q.selectCols +
		"\n" + in + "FROM " + from +
		"\n" + in + "WHERE " + where +
		"\n" + in + "GROUP BY " + q.groupBy +
		"\n" + in + "ORDER BY " + q.orderBy
}

// weightedCapWeightSelect is the multi-line cap_weight SUM expression shared by all
// four weighted builders. It is appended after the leading hidden columns, joined by
// ",\n" + the 8-space weighted indent, matching the original literals exactly.
const weightedCapWeightSelect = `SUM(CASE WHEN m.nominal_capacity > 0 AND m.capacity > 0
                        THEN CAST(m.nominal_capacity AS REAL) / CAST(m.capacity AS REAL)
                        ELSE 1.0 END) AS cap_weight`

// BuildTimePivotQuery generates a time-bucket x secondary-dimension query
// returning three result columns (bucket, grp, count), grouped by bucket and grp.
// Returns (query, args, error).
func BuildTimePivotQuery(def ReportDefinition, baseWhere string, args []any) (string, []any, error) {
	col2 := GroupByColumn(def.SecondaryGroupBy)
	if col2 == "" {
		return "", nil, fmt.Errorf("unsupported secondary group-by dimension %q", def.SecondaryGroupBy)
	}

	format := TimeBucketFormat(def.TimeBucket, def.CustomBucketUnit)
	bucketExpr := "strftime('" + format + "', datetime(m.start_timestamp, 'unixepoch'))"
	needsArtifactJoin := strings.HasPrefix(col2, "d.") || strings.Contains(baseWhere, "d.")

	out := make([]any, len(args))
	copy(out, args)

	var extraWhere []string
	if def.TimeBucket == "custom" {
		cond, modifier := CustomWindowCondition(def.CustomBucketN, def.CustomBucketUnit)
		if cond != "" {
			extraWhere = append(extraWhere, cond)
			out = append(out, modifier)
		}
	}

	query := queryBuilder{
		indent:      "            ",
		selectCols:  fmt.Sprintf("%s AS bucket, CAST(%s AS TEXT) AS grp, COUNT(*) AS count", bucketExpr, col2),
		artifactSrc: needsArtifactJoin,
		where:       baseWhere,
		extraWhere:  extraWhere,
		groupBy:     "bucket, grp",
		orderBy:     "bucket ASC, grp ASC",
	}.build()
	return query, out, nil
}

// FamilyWeightClause builds a SQL fragment filtering artifact_drops to a set of afx_ids.
// ids should be eiafx.FamilyAFXIds[familyId]. Returns ("", nil) if ids is empty.
func FamilyWeightClause(ids []int) (string, []any) {
	if len(ids) == 0 {
		return "", nil
	}
	placeholders := make([]string, len(ids))
	args := make([]any, len(ids))
	for i, id := range ids {
		placeholders[i] = "?"
		args[i] = id
	}
	return "d.artifact_id IN (" + strings.Join(placeholders, ", ") + ")", args
}

// BuildWeightedAggregateQuery generates a 1D aggregate query with hidden d.artifact_id
// and d.level columns so execute.go can apply per-tier crafting weights to the count.
func BuildWeightedAggregateQuery(def ReportDefinition, baseWhere string, baseArgs []any, fwClause string, fwArgs []any) (string, []any) {
	args := make([]any, len(baseArgs))
	copy(args, baseArgs)
	args = append(args, fwArgs...)

	groupCol := GroupByColumn(def.GroupBy)
	where := baseWhere + " AND " + fwClause

	query := queryBuilder{
		indent:      "        ",
		selectCols:  fmt.Sprintf("CAST(%s AS TEXT), CAST(d.artifact_id AS INTEGER), CAST(d.level AS INTEGER),\n               %s", groupCol, weightedCapWeightSelect),
		artifactSrc: true,
		where:       where,
		groupBy:     fmt.Sprintf("%s, d.artifact_id, d.level", groupCol),
		orderBy:     "cap_weight DESC",
	}.build()

	return query, args
}

// BuildWeightedPivotQuery generates a 2D pivot query with hidden d.artifact_id and d.level
// columns for per-tier weight accumulation. Returns an error if either dimension is invalid.
func BuildWeightedPivotQuery(def ReportDefinition, baseWhere string, baseArgs []any, fwClause string, fwArgs []any) (string, []any, error) {
	col1 := GroupByColumn(def.GroupBy)
	col2 := GroupByColumn(def.SecondaryGroupBy)
	if col1 == "" {
		return "", nil, fmt.Errorf("unsupported group-by dimension %q", def.GroupBy)
	}
	if col2 == "" {
		return "", nil, fmt.Errorf("unsupported secondary group-by dimension %q", def.SecondaryGroupBy)
	}

	args := make([]any, len(baseArgs))
	copy(args, baseArgs)
	args = append(args, fwArgs...)

	where := baseWhere + " AND " + fwClause
	query := queryBuilder{
		indent:      "        ",
		selectCols:  fmt.Sprintf("CAST(%s AS TEXT), CAST(%s AS TEXT), CAST(d.artifact_id AS INTEGER), CAST(d.level AS INTEGER),\n               %s", col1, col2, weightedCapWeightSelect),
		artifactSrc: true,
		where:       where,
		groupBy:     fmt.Sprintf("%s, %s, d.artifact_id, d.level", col1, col2),
		orderBy:     fmt.Sprintf("%s, %s", col1, col2),
	}.build()

	return query, args, nil
}

// BuildWeightedTimeSeriesQuery generates a time-bucketed query with hidden d.artifact_id
// and d.level columns for per-tier weight accumulation.
func BuildWeightedTimeSeriesQuery(def ReportDefinition, baseWhere string, baseArgs []any, fwClause string, fwArgs []any) (string, []any) {
	args := make([]any, len(baseArgs))
	copy(args, baseArgs)
	args = append(args, fwArgs...)

	format := TimeBucketFormat(def.TimeBucket, def.CustomBucketUnit)
	bucketExpr := "strftime('" + format + "', datetime(m.start_timestamp, 'unixepoch'))"
	where := baseWhere + " AND " + fwClause

	var extraWhere []string
	if def.TimeBucket == "custom" {
		cond, modifier := CustomWindowCondition(def.CustomBucketN, def.CustomBucketUnit)
		if cond != "" {
			extraWhere = append(extraWhere, cond)
			args = append(args, modifier)
		}
	}

	query := queryBuilder{
		indent:      "        ",
		selectCols:  fmt.Sprintf("%s AS bucket, CAST(d.artifact_id AS INTEGER), CAST(d.level AS INTEGER),\n               %s", bucketExpr, weightedCapWeightSelect),
		artifactSrc: true,
		where:       where,
		extraWhere:  extraWhere,
		groupBy:     "bucket, d.artifact_id, d.level",
		orderBy:     "bucket ASC",
	}.build()

	return query, args
}

// BuildWeightedTimePivotQuery generates a time-bucket x secondary-dimension query
// with hidden artifact_id/level columns for weight accumulation.
func BuildWeightedTimePivotQuery(def ReportDefinition, baseWhere string, baseArgs []any, fwClause string, fwArgs []any) (string, []any, error) {
	col2 := GroupByColumn(def.SecondaryGroupBy)
	if col2 == "" {
		return "", nil, fmt.Errorf("unsupported secondary group-by dimension %q", def.SecondaryGroupBy)
	}

	args := make([]any, len(baseArgs))
	copy(args, baseArgs)
	args = append(args, fwArgs...)

	format := TimeBucketFormat(def.TimeBucket, def.CustomBucketUnit)
	bucketExpr := "strftime('" + format + "', datetime(m.start_timestamp, 'unixepoch'))"
	where := baseWhere + " AND " + fwClause

	var extraWhere []string
	if def.TimeBucket == "custom" {
		cond, modifier := CustomWindowCondition(def.CustomBucketN, def.CustomBucketUnit)
		if cond != "" {
			extraWhere = append(extraWhere, cond)
			args = append(args, modifier)
		}
	}

	query := queryBuilder{
		indent:      "        ",
		selectCols:  fmt.Sprintf("%s AS bucket, CAST(%s AS TEXT) AS grp, CAST(d.artifact_id AS INTEGER), CAST(d.level AS INTEGER),\n               %s", bucketExpr, col2, weightedCapWeightSelect),
		artifactSrc: true,
		where:       where,
		extraWhere:  extraWhere,
		groupBy:     "bucket, grp, d.artifact_id, d.level",
		orderBy:     "bucket ASC, grp ASC",
	}.build()

	return query, args, nil
}
