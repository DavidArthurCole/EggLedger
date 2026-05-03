package reports

import (
	"context"
	"database/sql"
	"fmt"
	"sort"
	"strings"

	"github.com/DavidArthurCole/EggLedger/eiafx"
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
		return executeTimePivotReport(ctx, def, baseWhere, args, baseArgs)
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

// apply2DPctNormalization normalizes a row-major matrix in-place.
// mode must be "row_pct", "col_pct", or "global_pct"; anything else is a no-op.
func apply2DPctNormalization(vals []float64, nR, nC int, mode string) {
	if nR == 0 || nC == 0 {
		return
	}
	switch mode {
	case "row_pct":
		for r := 0; r < nR; r++ {
			var rowSum float64
			for c := 0; c < nC; c++ {
				rowSum += vals[r*nC+c]
			}
			if rowSum > 0 {
				for c := 0; c < nC; c++ {
					vals[r*nC+c] = vals[r*nC+c] / rowSum * 100
				}
			}
		}
	case "col_pct":
		for c := 0; c < nC; c++ {
			var colSum float64
			for r := 0; r < nR; r++ {
				colSum += vals[r*nC+c]
			}
			if colSum > 0 {
				for r := 0; r < nR; r++ {
					vals[r*nC+c] = vals[r*nC+c] / colSum * 100
				}
			}
		}
	case "global_pct":
		var total float64
		for _, v := range vals {
			total += v
		}
		if total > 0 {
			for i := range vals {
				vals[i] = vals[i] / total * 100
			}
		}
	}
}

func buildPivotQuery(def ReportDefinition, baseWhere string, args []interface{}) (string, []interface{}, error) {
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

	type labelEntry struct {
		display string
		rawVal  string
	}

	var rowEntries []labelEntry
	var colEntries []labelEntry

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
			rowEntries = append(rowEntries, labelEntry{display: rowLabel, rawVal: rawRow})
		}
		if _, ok := colSet[colLabel]; !ok {
			colSet[colLabel] = struct{}{}
			colEntries = append(colEntries, labelEntry{display: colLabel, rawVal: rawCol})
		}
		if cells[rowLabel] == nil {
			cells[rowLabel] = map[string]float64{}
		}
		cells[rowLabel][colLabel] = float64(count)
	}
	if err := rows.Err(); err != nil {
		return ReportResult{}, wrap(err)
	}

	sort.SliceStable(rowEntries, func(i, j int) bool {
		return LabelSortLess(def.GroupBy, rowEntries[i].rawVal, rowEntries[j].rawVal)
	})
	sort.SliceStable(colEntries, func(i, j int) bool {
		return LabelSortLess(def.SecondaryGroupBy, colEntries[i].rawVal, colEntries[j].rawVal)
	})

	rowLabels := make([]string, len(rowEntries))
	colLabels := make([]string, len(colEntries))
	for i, e := range rowEntries {
		rowLabels[i] = e.display
	}
	for i, e := range colEntries {
		colLabels[i] = e.display
	}

	matrixValues := make([]float64, len(rowLabels)*len(colLabels))
	for r, row := range rowLabels {
		for c, col := range colLabels {
			matrixValues[r*len(colLabels)+c] = cells[row][col]
		}
	}

	pctMode := def.NormalizeBy
	if pctMode == "row_pct" || pctMode == "col_pct" || pctMode == "global_pct" {
		apply2DPctNormalization(matrixValues, len(rowLabels), len(colLabels), pctMode)
	} else if pctMode != "" && pctMode != "none" {
		col1 := GroupByColumn(def.GroupBy)
		col2 := GroupByColumn(def.SecondaryGroupBy)
		if col1 != "" && !strings.HasPrefix(col1, "d.") && col2 != "" && !strings.HasPrefix(col2, "d.") {
			var denomQuery string
			if pctMode == "airtime" {
				denomQuery = fmt.Sprintf(
					`SELECT CAST(%s AS TEXT), CAST(%s AS TEXT), SUM(CAST(m.return_timestamp - m.start_timestamp AS REAL) / 3600.0) FROM mission m WHERE %s GROUP BY %s, %s`,
					col1, col2, baseWhere, col1, col2,
				)
			} else {
				denomQuery = fmt.Sprintf(
					`SELECT CAST(%s AS TEXT), CAST(%s AS TEXT), COUNT(*) FROM mission m WHERE %s GROUP BY %s, %s`,
					col1, col2, baseWhere, col1, col2,
				)
			}
			denomRows, err := _missionDB.QueryContext(ctx, denomQuery, baseArgs...)
			if err != nil {
				return ReportResult{}, wrap(err)
			}
			defer denomRows.Close()
			denomMap := map[string]map[string]float64{}
			for denomRows.Next() {
				var rawKey1, rawKey2 string
				var denom float64
				if err := denomRows.Scan(&rawKey1, &rawKey2, &denom); err != nil {
					return ReportResult{}, wrap(err)
				}
				k1 := FormatLabel(def.GroupBy, rawKey1)
				k2 := FormatLabel(def.SecondaryGroupBy, rawKey2)
				if denomMap[k1] == nil {
					denomMap[k1] = map[string]float64{}
				}
				denomMap[k1][k2] = denom
			}
			if err := denomRows.Err(); err != nil {
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

	rawRowLabels := make([]string, len(rowEntries))
	rawColLabels := make([]string, len(colEntries))
	for i, e := range rowEntries {
		rawRowLabels[i] = e.rawVal
	}
	for i, e := range colEntries {
		rawColLabels[i] = e.rawVal
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

func executeTimePivotReport(ctx context.Context, def ReportDefinition, baseWhere string, args []interface{}, baseArgs []interface{}) (ReportResult, error) {
	// baseArgs is the pre-filter arg snapshot; accepted for caller consistency but not used
	// since time-series pivots do not support airtime/launches normalization.
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

	var bucketLabels []string
	bucketSet := map[string]struct{}{}
	groupSet := map[string]struct{}{}
	cells := map[string]map[string]float64{}

	type grpEntry struct {
		display string
		rawVal  string
	}
	var grpEntries []grpEntry

	for rows.Next() {
		var rawBucket, rawGrp string
		var count int64
		if err := rows.Scan(&rawBucket, &rawGrp, &count); err != nil {
			return ReportResult{}, wrap(err)
		}
		grpLabel := FormatLabel(def.SecondaryGroupBy, rawGrp)
		if _, ok := bucketSet[rawBucket]; !ok {
			bucketSet[rawBucket] = struct{}{}
			bucketLabels = append(bucketLabels, rawBucket)
		}
		if _, ok := groupSet[grpLabel]; !ok {
			groupSet[grpLabel] = struct{}{}
			grpEntries = append(grpEntries, grpEntry{display: grpLabel, rawVal: rawGrp})
		}
		if cells[rawBucket] == nil {
			cells[rawBucket] = map[string]float64{}
		}
		cells[rawBucket][grpLabel] = float64(count)
	}
	if err := rows.Err(); err != nil {
		return ReportResult{}, wrap(err)
	}

	sort.SliceStable(grpEntries, func(i, j int) bool {
		return LabelSortLess(def.SecondaryGroupBy, grpEntries[i].rawVal, grpEntries[j].rawVal)
	})
	groupLabels := make([]string, len(grpEntries))
	for i, e := range grpEntries {
		groupLabels[i] = e.display
	}

	nR := len(bucketLabels)
	nC := len(groupLabels)
	matrixValues := make([]float64, nR*nC)
	for r, bucket := range bucketLabels {
		for c, grp := range groupLabels {
			if cells[bucket] != nil {
				matrixValues[r*nC+c] = cells[bucket][grp]
			}
		}
	}

	if nR > 0 {
		bucketLabels, matrixValues = fillTimePivotGaps(def.TimeBucket, def.CustomBucketUnit, bucketLabels, nC, matrixValues)
		nR = len(bucketLabels)
	}

	pctMode := def.NormalizeBy
	if pctMode == "row_pct" || pctMode == "col_pct" || pctMode == "global_pct" {
		apply2DPctNormalization(matrixValues, nR, nC, pctMode)
	}

	rawGrpLabels := make([]string, len(grpEntries))
	for i, e := range grpEntries {
		rawGrpLabels[i] = e.rawVal
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
