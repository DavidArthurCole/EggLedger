package reports

import (
	"context"
	"database/sql"
	"fmt"

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

	var labels []string
	var values []int64
	for rows.Next() {
		var rawLabel string
		var count int64
		if err := rows.Scan(&rawLabel, &count); err != nil {
			return ReportResult{}, wrap(err)
		}
		labels = append(labels, FormatLabel(def.GroupBy, rawLabel))
		values = append(values, count)
	}
	if err := rows.Err(); err != nil {
		return ReportResult{}, wrap(err)
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
