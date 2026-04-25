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
