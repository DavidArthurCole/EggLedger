package reports

import (
	"context"
	"database/sql"
	"fmt"
	"strings"

	"github.com/DavidArthurCole/EggLedger/eiafx"
	"github.com/DavidArthurCole/EggLedger/util"
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

	whereClause, filterArgs := BuildWhereClause(def.Filters)
	baseWhere := "m.player_id = ?"
	if whereClause != "" {
		baseWhere += " AND " + whereClause
	}
	args := []any{def.AccountId}
	args = append(args, filterArgs...)
	baseArgs := make([]any, len(args))
	copy(baseArgs, args)

	if def.FamilyWeight != "" {
		ids := eiafx.FamilyAFXIds[def.FamilyWeight]
		fwClause, fwArgs := FamilyWeightClause(ids)
		if fwClause != "" {
			if def.SecondaryGroupBy != "" && def.Mode == "time_series" {
				return executeWeightedTimePivot(ctx, def, baseWhere, args, fwClause, fwArgs)
			}
			if def.SecondaryGroupBy != "" {
				return executeWeightedPivot(ctx, def, baseWhere, args, baseArgs, fwClause, fwArgs)
			}
			if def.Mode == "time_series" {
				return executeWeightedTimeSeries(ctx, def, baseWhere, args, fwClause, fwArgs)
			}
			return executeWeightedAggregate(ctx, def, baseWhere, args, baseArgs, fwClause, fwArgs)
		}
	}

	if def.SecondaryGroupBy != "" && def.Mode == "time_series" {
		return executeTimePivotReport(ctx, def, baseWhere, args)
	}
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

	if def.Mode == "time_series" && len(rawLabels) > 0 {
		rawLabels, values = fillTimeSeriesGaps(def.TimeBucket, def.CustomBucketUnit, rawLabels, values)
		labels = make([]string, len(rawLabels))
		for i, rl := range rawLabels {
			labels[i] = FormatLabel(def.GroupBy, rl)
		}
	}

	if def.NormalizeBy != "" && def.NormalizeBy != "none" && def.Mode == "aggregate" {
		groupCol := GroupByColumn(def.GroupBy)
		if groupCol == "" || strings.HasPrefix(groupCol, "d.") {
			return ReportResult{Labels: labels, Values: values, Weight: def.Weight}, nil
		}
		denomMap, err := denom1D(ctx, groupCol, def.NormalizeBy, baseWhere, baseArgs)
		if err != nil {
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

func buildPivotQuery(def ReportDefinition, baseWhere string, args []any) (string, []any, error) {
	if def.TimeBucket != "" && def.Mode == "time_series" {
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
	out := make([]any, len(args))
	copy(out, args)
	needsArtifactJoin := strings.HasPrefix(col1, "d.") || strings.HasPrefix(col2, "d.")
	query := queryBuilder{
		indent:      "            ",
		selectCols:  fmt.Sprintf("CAST(%s AS TEXT), CAST(%s AS TEXT), COUNT(*) AS count", col1, col2),
		artifactSrc: needsArtifactJoin,
		where:       baseWhere,
		groupBy:     fmt.Sprintf("%s, %s", col1, col2),
		orderBy:     fmt.Sprintf("%s, %s", col1, col2),
	}.build()
	return query, out, nil
}

func executePivotReport(ctx context.Context, def ReportDefinition, baseWhere string, args []any, baseArgs []any) (ReportResult, error) {
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

	accum := newPivotAccum()
	for rows.Next() {
		var rawRow, rawCol string
		var count int64
		if err := rows.Scan(&rawRow, &rawCol, &count); err != nil {
			return ReportResult{}, wrap(err)
		}
		rowLabel := FormatLabel(def.GroupBy, rawRow)
		colLabel := FormatLabel(def.SecondaryGroupBy, rawCol)
		accum.add(rowLabel, rawRow, colLabel, rawCol, float64(count))
	}
	if err := rows.Err(); err != nil {
		return ReportResult{}, wrap(err)
	}

	rowLabels, colLabels, rawRowLabels, rawColLabels, matrixValues :=
		accum.finalize(true, true, def.GroupBy, def.SecondaryGroupBy)

	pctMode := def.NormalizeBy
	if pctMode == "row_pct" || pctMode == "col_pct" || pctMode == "global_pct" {
		util.Apply2DPctNormalization(matrixValues, len(rowLabels), len(colLabels), pctMode)
	} else if pctMode != "" && pctMode != "none" {
		col1 := GroupByColumn(def.GroupBy)
		col2 := GroupByColumn(def.SecondaryGroupBy)
		if col1 != "" && !strings.HasPrefix(col1, "d.") && col2 != "" && !strings.HasPrefix(col2, "d.") {
			denomMap, err := denom2D(ctx, def, col1, col2, pctMode, baseWhere, baseArgs)
			if err != nil {
				return ReportResult{}, wrap(err)
			}
			nC := len(colLabels)
			for r, row := range rowLabels {
				for c, col := range colLabels {
					if d := denomMap[row][col]; d > 0 {
						matrixValues[r*nC+c] /= d
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
		RawRowLabels: rawRowLabels,
		RawColLabels: rawColLabels,
	}, nil
}

func executeTimePivotReport(ctx context.Context, def ReportDefinition, baseWhere string, args []any) (ReportResult, error) {
	// Time-series pivots do not support airtime/launches normalization, so no
	// pre-filter arg snapshot (baseArgs) is needed here.
	action := fmt.Sprintf("execute time pivot report %s", def.Id)
	wrap := func(err error) error { return errors.Wrap(err, action) }

	query, queryArgs, err := BuildTimePivotQuery(def, baseWhere, args)
	if err != nil {
		return ReportResult{}, wrap(err)
	}

	rows, err := _missionDB.QueryContext(ctx, query, queryArgs...)
	if err != nil {
		return ReportResult{}, wrap(err)
	}
	defer rows.Close()

	accum := newPivotAccum()
	for rows.Next() {
		var rawBucket, rawGrp string
		var count int64
		if err := rows.Scan(&rawBucket, &rawGrp, &count); err != nil {
			return ReportResult{}, wrap(err)
		}
		grpLabel := FormatLabel(def.SecondaryGroupBy, rawGrp)
		// Bucket rows are not FormatLabel'd: the raw bucket string is the display.
		accum.add(rawBucket, rawBucket, grpLabel, rawGrp, float64(count))
	}
	if err := rows.Err(); err != nil {
		return ReportResult{}, wrap(err)
	}

	// Time pivots leave the bucket (row) axis in query ASC order; only the group
	// (col) axis is sorted.
	bucketLabels, groupLabels, _, rawGrpLabels, matrixValues :=
		accum.finalize(false, true, def.GroupBy, def.SecondaryGroupBy)

	nR := len(bucketLabels)
	nC := len(groupLabels)

	if nR > 0 {
		bucketLabels, matrixValues = fillTimePivotGaps(def.TimeBucket, def.CustomBucketUnit, bucketLabels, nC, matrixValues)
		nR = len(bucketLabels)
	}

	pctMode := def.NormalizeBy
	if pctMode == "row_pct" || pctMode == "col_pct" || pctMode == "global_pct" {
		util.Apply2DPctNormalization(matrixValues, nR, nC, pctMode)
	}

	return ReportResult{
		RowLabels:    bucketLabels,
		ColLabels:    groupLabels,
		MatrixValues: matrixValues,
		Is2D:         true,
		Weight:       def.Weight,
		RawRowLabels: bucketLabels,
		RawColLabels: rawGrpLabels,
	}, nil
}

func buildAggregateQuery(def ReportDefinition, baseWhere string, baseArgs []any) (string, []any) {
	args := make([]any, len(baseArgs))
	copy(args, baseArgs)

	groupCol := GroupByColumn(def.GroupBy)
	query := queryBuilder{
		indent:      "            ",
		selectCols:  fmt.Sprintf("CAST(%s AS TEXT), COUNT(*) AS count", groupCol),
		artifactSrc: def.Subject == "artifacts",
		where:       baseWhere,
		groupBy:     groupCol,
		orderBy:     "count DESC",
	}.build()
	return query, args
}

func buildTimeSeriesQuery(def ReportDefinition, baseWhere string, baseArgs []any) (string, []any) {
	args := make([]any, len(baseArgs))
	copy(args, baseArgs)

	format := TimeBucketFormat(def.TimeBucket, def.CustomBucketUnit)
	bucketExpr := "strftime('" + format + "', datetime(m.start_timestamp, 'unixepoch'))"

	var extraWhere []string
	if def.TimeBucket == "custom" {
		cond, modifier := CustomWindowCondition(def.CustomBucketN, def.CustomBucketUnit)
		if cond != "" {
			extraWhere = append(extraWhere, cond)
			args = append(args, modifier)
		}
	}

	query := queryBuilder{
		indent:      "            ",
		selectCols:  fmt.Sprintf("%s AS bucket, COUNT(*) AS count", bucketExpr),
		artifactSrc: def.Subject == "artifacts",
		where:       baseWhere,
		extraWhere:  extraWhere,
		groupBy:     "bucket",
		orderBy:     "bucket ASC",
	}.build()
	return query, args
}
