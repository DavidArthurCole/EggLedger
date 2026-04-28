package reports

import (
	"context"
	"fmt"
	"sort"
	"strings"

	"github.com/DavidArthurCole/EggLedger/eiafx"
	"github.com/pkg/errors"
)

func executeWeightedAggregate(ctx context.Context, def ReportDefinition, baseWhere string, args []interface{}, baseArgs []interface{}, fwClause string, fwArgs []interface{}) (ReportResult, error) {
	action := fmt.Sprintf("execute weighted aggregate report %s", def.Id)
	wrap := func(err error) error { return errors.Wrap(err, action) }

	query, queryArgs := BuildWeightedAggregateQuery(def, baseWhere, args, fwClause, fwArgs)
	rows, err := _missionDB.QueryContext(ctx, query, queryArgs...)
	if err != nil {
		return ReportResult{}, wrap(err)
	}
	defer rows.Close()

	accum := map[string]float64{}
	var rawOrder []string
	seen := map[string]bool{}

	for rows.Next() {
		var rawLabel string
		var artifactId, level int64
		var capWeight float64
		if err := rows.Scan(&rawLabel, &artifactId, &level, &capWeight); err != nil {
			return ReportResult{}, wrap(err)
		}
		w := eiafx.CraftingWeights[[2]int{int(artifactId), int(level)}]
		if w == 0 {
			// Zero means this key was left as the cycle-sentinel in CraftingWeights
			// (recipe graph had a cycle). Fall back to 1 so the row is not silently dropped.
			w = 1
		}
		if !seen[rawLabel] {
			seen[rawLabel] = true
			rawOrder = append(rawOrder, rawLabel)
		}
		accum[rawLabel] += capWeight * w
	}
	if err := rows.Err(); err != nil {
		return ReportResult{}, wrap(err)
	}

	labels := make([]string, len(rawOrder))
	floatValues := make([]float64, len(rawOrder))
	for i, raw := range rawOrder {
		labels[i] = FormatLabel(def.GroupBy, raw)
		floatValues[i] = accum[raw]
	}

	if def.NormalizeBy != "" && def.NormalizeBy != "none" && def.Mode == "aggregate" {
		groupCol := GroupByColumn(def.GroupBy)
		if groupCol != "" && !strings.HasPrefix(groupCol, "d.") {
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
			denomMap := map[string]float64{}
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
			for i, raw := range rawOrder {
				if d := denomMap[raw]; d > 0 {
					floatValues[i] /= d
				}
			}
		}
	}

	// Sort descending by final (post-normalization) value so bar order is correct
	// regardless of whether normalization changed the relative ranking.
	sort.SliceStable(rawOrder, func(i, j int) bool {
		return floatValues[i] > floatValues[j]
	})
	// Re-apply label ordering to match the sorted rawOrder.
	for i, raw := range rawOrder {
		labels[i] = FormatLabel(def.GroupBy, raw)
	}

	return ReportResult{
		Labels:      labels,
		FloatValues: floatValues,
		IsFloat:     true,
		Weight:      def.Weight,
	}, nil
}

func executeWeightedPivot(ctx context.Context, def ReportDefinition, baseWhere string, args []interface{}, baseArgs []interface{}, fwClause string, fwArgs []interface{}) (ReportResult, error) {
	action := fmt.Sprintf("execute weighted pivot report %s", def.Id)
	wrap := func(err error) error { return errors.Wrap(err, action) }

	query, queryArgs, err := BuildWeightedPivotQuery(def, baseWhere, args, fwClause, fwArgs)
	if err != nil {
		return ReportResult{}, wrap(err)
	}

	rows, err := _missionDB.QueryContext(ctx, query, queryArgs...)
	if err != nil {
		return ReportResult{}, wrap(err)
	}
	defer rows.Close()

	type labelEntry struct {
		display string
		rawVal  string
	}

	rowSet := map[string]struct{}{}
	colSet := map[string]struct{}{}
	cells := map[string]map[string]float64{}
	var rowEntries []labelEntry
	var colEntries []labelEntry

	for rows.Next() {
		var rawRow, rawCol string
		var artifactId, level int64
		var capWeight float64
		if err := rows.Scan(&rawRow, &rawCol, &artifactId, &level, &capWeight); err != nil {
			return ReportResult{}, wrap(err)
		}
		w := eiafx.CraftingWeights[[2]int{int(artifactId), int(level)}]
		if w == 0 {
			// Zero means this key was left as the cycle-sentinel in CraftingWeights
			// (recipe graph had a cycle). Fall back to 1 so the row is not silently dropped.
			w = 1
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
		cells[rowLabel][colLabel] += capWeight * w
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

	// Always compute mission counts for weighted pivots so the frontend can apply
	// a minimum sample size threshold regardless of normalization mode.
	col1mc := GroupByColumn(def.GroupBy)
	col2mc := GroupByColumn(def.SecondaryGroupBy)
	mcMap := map[string]map[string]float64{}
	if col1mc != "" && !strings.HasPrefix(col1mc, "d.") && col2mc != "" && !strings.HasPrefix(col2mc, "d.") {
		mcQuery := fmt.Sprintf(
			`SELECT CAST(%s AS TEXT), CAST(%s AS TEXT), COUNT(*) FROM mission m WHERE %s GROUP BY %s, %s`,
			col1mc, col2mc, baseWhere, col1mc, col2mc,
		)
		mcRows, err := _missionDB.QueryContext(ctx, mcQuery, baseArgs...)
		if err != nil {
			return ReportResult{}, wrap(err)
		}
		defer mcRows.Close()
		for mcRows.Next() {
			var rawKey1, rawKey2 string
			var cnt float64
			if err := mcRows.Scan(&rawKey1, &rawKey2, &cnt); err != nil {
				return ReportResult{}, wrap(err)
			}
			k1 := FormatLabel(def.GroupBy, rawKey1)
			k2 := FormatLabel(def.SecondaryGroupBy, rawKey2)
			if mcMap[k1] == nil {
				mcMap[k1] = map[string]float64{}
			}
			mcMap[k1][k2] = cnt
		}
		if err := mcRows.Err(); err != nil {
			return ReportResult{}, wrap(err)
		}
	}

	missionCountMatrix := make([]int64, len(rowLabels)*len(colLabels))
	for r, row := range rowLabels {
		for c, col := range colLabels {
			missionCountMatrix[r*len(colLabels)+c] = int64(mcMap[row][col])
		}
	}

	pctMode := def.NormalizeBy
	var rawPerMissionValues []float64
	if pctMode == "row_pct" || pctMode == "col_pct" || pctMode == "global_pct" {
		apply2DPctNormalization(matrixValues, len(rowLabels), len(colLabels), pctMode)
	} else if pctMode != "" && pctMode != "none" {
		col1 := GroupByColumn(def.GroupBy)
		col2 := GroupByColumn(def.SecondaryGroupBy)
		if col1 != "" && !strings.HasPrefix(col1, "d.") && col2 != "" && !strings.HasPrefix(col2, "d.") {
			if pctMode == "airtime" {
				// Compute per-mission values alongside airtime values so ratio
				// comparisons against community data (which uses mission counts)
				// are apples-to-apples.
				rawPerMissionValues = make([]float64, len(rowLabels)*len(colLabels))
				copy(rawPerMissionValues, matrixValues)
				for r, row := range rowLabels {
					for c, col := range colLabels {
						if d := mcMap[row][col]; d > 0 {
							rawPerMissionValues[r*len(colLabels)+c] /= d
						}
					}
				}
			}

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
			for r, row := range rowLabels {
				for c, col := range colLabels {
					if d := denomMap[row][col]; d > 0 {
						matrixValues[r*len(colLabels)+c] /= d
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
		RowLabels:           rowLabels,
		ColLabels:           colLabels,
		MatrixValues:        matrixValues,
		Is2D:                true,
		Weight:              def.Weight,
		RawRowLabels:        rawRowLabels,
		RawColLabels:        rawColLabels,
		RawPerMissionValues: rawPerMissionValues,
		MissionCountMatrix:  missionCountMatrix,
	}, nil
}

func executeWeightedTimeSeries(ctx context.Context, def ReportDefinition, baseWhere string, args []interface{}, fwClause string, fwArgs []interface{}) (ReportResult, error) {
	action := fmt.Sprintf("execute weighted time series report %s", def.Id)
	wrap := func(err error) error { return errors.Wrap(err, action) }

	query, queryArgs := BuildWeightedTimeSeriesQuery(def, baseWhere, args, fwClause, fwArgs)
	rows, err := _missionDB.QueryContext(ctx, query, queryArgs...)
	if err != nil {
		return ReportResult{}, wrap(err)
	}
	defer rows.Close()

	accum := map[string]float64{}
	var buckets []string
	seen := map[string]bool{}

	for rows.Next() {
		var rawBucket string
		var artifactId, level int64
		var capWeight float64
		if err := rows.Scan(&rawBucket, &artifactId, &level, &capWeight); err != nil {
			return ReportResult{}, wrap(err)
		}
		w := eiafx.CraftingWeights[[2]int{int(artifactId), int(level)}]
		if w == 0 {
			// Zero means this key was left as the cycle-sentinel in CraftingWeights
			// (recipe graph had a cycle). Fall back to 1 so the row is not silently dropped.
			w = 1
		}
		if !seen[rawBucket] {
			seen[rawBucket] = true
			buckets = append(buckets, rawBucket)
		}
		accum[rawBucket] += capWeight * w
	}
	if err := rows.Err(); err != nil {
		return ReportResult{}, wrap(err)
	}

	floatValues := make([]float64, len(buckets))
	for i, b := range buckets {
		floatValues[i] = accum[b]
	}

	return ReportResult{
		Labels:      buckets,
		FloatValues: floatValues,
		IsFloat:     true,
		Weight:      def.Weight,
	}, nil
}

func executeWeightedTimePivot(ctx context.Context, def ReportDefinition, baseWhere string, args []interface{}, fwClause string, fwArgs []interface{}) (ReportResult, error) {
	action := fmt.Sprintf("execute weighted time pivot report %s", def.Id)
	wrap := func(err error) error { return errors.Wrap(err, action) }

	query, queryArgs, err := BuildWeightedTimePivotQuery(def, baseWhere, args, fwClause, fwArgs)
	if err != nil {
		return ReportResult{}, wrap(err)
	}

	rows, err := _missionDB.QueryContext(ctx, query, queryArgs...)
	if err != nil {
		return ReportResult{}, wrap(err)
	}
	defer rows.Close()

	type grpEntry struct {
		display string
		rawVal  string
	}

	var bucketLabels []string
	bucketSet := map[string]struct{}{}
	groupSet := map[string]struct{}{}
	cells := map[string]map[string]float64{}
	var grpEntries []grpEntry

	for rows.Next() {
		var rawBucket, rawGrp string
		var artifactId, level int64
		var capWeight float64
		if err := rows.Scan(&rawBucket, &rawGrp, &artifactId, &level, &capWeight); err != nil {
			return ReportResult{}, wrap(err)
		}
		w := eiafx.CraftingWeights[[2]int{int(artifactId), int(level)}]
		if w == 0 {
			// Zero means this key was left as the cycle-sentinel in CraftingWeights
			// (recipe graph had a cycle). Fall back to 1 so the row is not silently dropped.
			w = 1
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
		cells[rawBucket][grpLabel] += capWeight * w
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
