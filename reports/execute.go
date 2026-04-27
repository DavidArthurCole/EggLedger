package reports

import (
	"context"
	"database/sql"
	"fmt"
	"strings"

	"github.com/pkg/errors"
)

var _missionDB *sql.DB

// SetMissionDB supplies the open missions DB handle to the query engine.
// Must be called during app init before any ExecuteReport call.
func SetMissionDB(db *sql.DB) {
	_missionDB = db
}

// ExecuteReport runs the report query and returns labeled results.
func ExecuteReport(ctx context.Context, def ReportDefinition) (ReportResult, error) {
	action := fmt.Sprintf("execute report %s", def.Id)
	wrap := func(err error) error { return errors.Wrap(err, action) }

	if _missionDB == nil {
		return ReportResult{}, wrap(errors.New("mission DB not initialized"))
	}

	whereClause, filterArgs := BuildWhereClause(def.Filters, def.Subject)
	baseWhere := "m.player_id = ?"
	if whereClause != "" {
		baseWhere += " AND " + whereClause
	}
	args := []interface{}{def.AccountId}
	args = append(args, filterArgs...)
	baseArgs := make([]interface{}, len(args))
	copy(baseArgs, args)

	if def.SecondaryGroupBy != "" {
		return executePivotReport(ctx, def, baseWhere, args, baseArgs)
	}

	var query string
	switch def.Mode {
	case "aggregate":
		query, args = buildAggregateQuery(def, baseWhere, args)
	case "time_series":
		query, args = buildTimeSeriesQuery(def, baseWhere, args)
	default:
		return ReportResult{}, wrap(fmt.Errorf("unknown mode %q", def.Mode))
	}

	rows, err := _missionDB.QueryContext(ctx, query, args...)
	if err != nil {
		return ReportResult{}, wrap(err)
	}
	defer rows.Close()

	rawLabels := make([]string, 0)
	labels := make([]string, 0)
	values := make([]int64, 0)
	for rows.Next() {
		var rawLabel string
		var count int64
		if err := rows.Scan(&rawLabel, &count); err != nil {
			return ReportResult{}, wrap(err)
		}
		rawLabels = append(rawLabels, rawLabel)
		labels = append(labels, FormatLabel(def.GroupBy, rawLabel))
		values = append(values, count)
	}
	if err := rows.Err(); err != nil {
		return ReportResult{}, wrap(err)
	}

	if def.NormalizeBy != "" && def.NormalizeBy != "none" && def.Mode == "aggregate" {
		groupCol := GroupByColumn(def.GroupBy)
		if groupCol == "" || strings.HasPrefix(groupCol, "d.") {
			return ReportResult{Labels: labels, Values: values, Weight: def.Weight}, nil
		}
		var denomQuery string
		if def.NormalizeBy == "airtime" {
			denomQuery = fmt.Sprintf(
				`SELECT CAST(%s AS TEXT), SUM(CAST(m.return_timestamp - m.start_timestamp AS REAL) / 3600.0) FROM mission m WHERE %s GROUP BY %s`,
				groupCol, baseWhere, groupCol,
			)
		} else {
			denomQuery = fmt.Sprintf(
				`SELECT CAST(%s AS TEXT), COUNT(*) FROM mission m WHERE %s GROUP BY %s`,
				groupCol, baseWhere, groupCol,
			)
		}
		denomRows, err := _missionDB.QueryContext(ctx, denomQuery, baseArgs...)
		if err != nil {
			return ReportResult{}, wrap(err)
		}
		defer denomRows.Close()
		denomMap := make(map[string]float64)
		for denomRows.Next() {
			var key string
			var denom float64
			if err := denomRows.Scan(&key, &denom); err != nil {
				return ReportResult{}, wrap(err)
			}
			denomMap[key] = denom
		}
		if err := denomRows.Err(); err != nil {
			return ReportResult{}, wrap(err)
		}

		floatValues := make([]float64, len(values))
		for i, rawLabel := range rawLabels {
			denom := denomMap[rawLabel]
			if denom > 0 {
				floatValues[i] = float64(values[i]) / denom
			}
		}
		return ReportResult{
			Labels:      labels,
			FloatValues: floatValues,
			IsFloat:     true,
			Weight:      def.Weight,
		}, nil
	}

	return ReportResult{
		Labels: labels,
		Values: values,
		Weight: def.Weight,
	}, nil
}

func buildPivotQuery(def ReportDefinition, baseWhere string, args []interface{}) (string, []interface{}, error) {
	if def.TimeBucket != "" {
		return "", nil, fmt.Errorf("time-series pivot is not supported; clear the time bucket or remove the secondary group-by")
	}
	col1 := GroupByColumn(def.GroupBy)
	col2 := GroupByColumn(def.SecondaryGroupBy)
	if col1 == "" {
		return "", nil, fmt.Errorf("unsupported group-by dimension %q", def.GroupBy)
	}
	if col2 == "" {
		return "", nil, fmt.Errorf("unsupported secondary group-by dimension %q", def.SecondaryGroupBy)
	}
	out := make([]interface{}, len(args))
	copy(out, args)
	needsArtifactJoin := strings.HasPrefix(col1, "d.") || strings.HasPrefix(col2, "d.")
	var query string
	if needsArtifactJoin {
		query = fmt.Sprintf(`
            SELECT CAST(%s AS TEXT), CAST(%s AS TEXT), COUNT(*) AS count
            FROM artifact_drops d
            JOIN mission m ON d.mission_id = m.mission_id AND d.player_id = m.player_id
            WHERE %s AND d.drop_index >= 0
            GROUP BY %s, %s
            ORDER BY %s, %s`, col1, col2, baseWhere, col1, col2, col1, col2)
	} else {
		query = fmt.Sprintf(`
            SELECT CAST(%s AS TEXT), CAST(%s AS TEXT), COUNT(*) AS count
            FROM mission m
            WHERE %s
            GROUP BY %s, %s
            ORDER BY %s, %s`, col1, col2, baseWhere, col1, col2, col1, col2)
	}
	return query, out, nil
}

func executePivotReport(ctx context.Context, def ReportDefinition, baseWhere string, args []interface{}, baseArgs []interface{}) (ReportResult, error) {
	action := fmt.Sprintf("execute pivot report %s", def.Id)
	wrap := func(err error) error { return errors.Wrap(err, action) }

	query, queryArgs, err := buildPivotQuery(def, baseWhere, args)
	if err != nil {
		return ReportResult{}, wrap(err)
	}

	rows, err := _missionDB.QueryContext(ctx, query, queryArgs...)
	if err != nil {
		return ReportResult{}, wrap(err)
	}
	defer rows.Close()

	rowSet := map[string]struct{}{}
	colSet := map[string]struct{}{}
	cells := map[string]map[string]float64{}
	var rowLabels []string
	var colLabels []string

	for rows.Next() {
		var rawRow, rawCol string
		var count int64
		if err := rows.Scan(&rawRow, &rawCol, &count); err != nil {
			return ReportResult{}, wrap(err)
		}
		rowLabel := FormatLabel(def.GroupBy, rawRow)
		colLabel := FormatLabel(def.SecondaryGroupBy, rawCol)
		if _, ok := rowSet[rowLabel]; !ok {
			rowSet[rowLabel] = struct{}{}
			rowLabels = append(rowLabels, rowLabel)
		}
		if _, ok := colSet[colLabel]; !ok {
			colSet[colLabel] = struct{}{}
			colLabels = append(colLabels, colLabel)
		}
		if cells[rowLabel] == nil {
			cells[rowLabel] = map[string]float64{}
		}
		cells[rowLabel][colLabel] = float64(count)
	}
	if err := rows.Err(); err != nil {
		return ReportResult{}, wrap(err)
	}

	matrixValues := make([]float64, len(rowLabels)*len(colLabels))
	for r, row := range rowLabels {
		for c, col := range colLabels {
			matrixValues[r*len(colLabels)+c] = cells[row][col]
		}
	}

	if def.NormalizeBy != "" && def.NormalizeBy != "none" {
		col1 := GroupByColumn(def.GroupBy)
		if col1 != "" && !strings.HasPrefix(col1, "d.") {
			var denomQuery string
			if def.NormalizeBy == "airtime" {
				denomQuery = fmt.Sprintf(
					`SELECT CAST(%s AS TEXT), SUM(CAST(m.return_timestamp - m.start_timestamp AS REAL) / 3600.0) FROM mission m WHERE %s GROUP BY %s`,
					col1, baseWhere, col1,
				)
			} else {
				denomQuery = fmt.Sprintf(
					`SELECT CAST(%s AS TEXT), COUNT(*) FROM mission m WHERE %s GROUP BY %s`,
					col1, baseWhere, col1,
				)
			}
			denomRows, err := _missionDB.QueryContext(ctx, denomQuery, baseArgs...)
			if err != nil {
				return ReportResult{}, wrap(err)
			}
			defer denomRows.Close()
			denomMap := map[string]float64{}
			for denomRows.Next() {
				var rawKey string
				var denom float64
				if err := denomRows.Scan(&rawKey, &denom); err != nil {
					return ReportResult{}, wrap(err)
				}
				denomMap[FormatLabel(def.GroupBy, rawKey)] = denom
			}
			if err := denomRows.Err(); err != nil {
				return ReportResult{}, wrap(err)
			}
			for r, row := range rowLabels {
				d := denomMap[row]
				if d > 0 {
					for c := range colLabels {
						matrixValues[r*len(colLabels)+c] /= d
					}
				}
			}
		}
	}

	return ReportResult{
		RowLabels:    rowLabels,
		ColLabels:    colLabels,
		MatrixValues: matrixValues,
		Is2D:         true,
		Weight:       def.Weight,
	}, nil
}

func buildAggregateQuery(def ReportDefinition, baseWhere string, baseArgs []interface{}) (string, []interface{}) {
	args := make([]interface{}, len(baseArgs))
	copy(args, baseArgs)

	groupCol := GroupByColumn(def.GroupBy)
	var query string

	if def.Subject == "artifacts" {
		query = fmt.Sprintf(`
            SELECT CAST(%s AS TEXT), COUNT(*) AS count
            FROM artifact_drops d
            JOIN mission m ON d.mission_id = m.mission_id AND d.player_id = m.player_id
            WHERE %s AND d.drop_index >= 0
            GROUP BY %s
            ORDER BY count DESC`, groupCol, baseWhere, groupCol)
	} else {
		query = fmt.Sprintf(`
            SELECT CAST(%s AS TEXT), COUNT(*) AS count
            FROM mission m
            WHERE %s
            GROUP BY %s
            ORDER BY count DESC`, groupCol, baseWhere, groupCol)
	}
	return query, args
}

func buildTimeSeriesQuery(def ReportDefinition, baseWhere string, baseArgs []interface{}) (string, []interface{}) {
	args := make([]interface{}, len(baseArgs))
	copy(args, baseArgs)

	format := TimeBucketFormat(def.TimeBucket, def.CustomBucketUnit)
	bucketExpr := "strftime('" + format + "', datetime(m.start_timestamp, 'unixepoch'))"

	windowWhere := ""
	if def.TimeBucket == "custom" {
		cond, modifier := CustomWindowCondition(def.CustomBucketN, def.CustomBucketUnit)
		if cond != "" {
			windowWhere = " AND " + cond
			args = append(args, modifier)
		}
	}

	var query string
	if def.Subject == "artifacts" {
		query = fmt.Sprintf(`
            SELECT %s AS bucket, COUNT(*) AS count
            FROM artifact_drops d
            JOIN mission m ON d.mission_id = m.mission_id AND d.player_id = m.player_id
            WHERE %s AND d.drop_index >= 0%s
            GROUP BY bucket
            ORDER BY bucket ASC`, bucketExpr, baseWhere, windowWhere)
	} else {
		query = fmt.Sprintf(`
            SELECT %s AS bucket, COUNT(*) AS count
            FROM mission m
            WHERE %s%s
            GROUP BY bucket
            ORDER BY bucket ASC`, bucketExpr, baseWhere, windowWhere)
	}
	return query, args
}
