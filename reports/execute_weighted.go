package reports

import (
	"context"
	"fmt"
	"sort"
	"strings"

	"github.com/DavidArthurCole/EggLedger/eiafx"
	"github.com/DavidArthurCole/EggLedger/util"
	"github.com/pkg/errors"
)

// craftingWeight returns the per-tier crafting weight for an artifact (id, level),
// falling back to 1 when the key is the cycle-sentinel (0) so the row is not dropped.
func craftingWeight(artifactId, level int64) float64 {
	w := eiafx.CraftingWeights[[2]int{int(artifactId), int(level)}]
	if w == 0 {
		return 1
	}
	return w
}

func executeWeightedAggregate(ctx context.Context, def ReportDefinition, baseWhere string, args []any, baseArgs []any, fwClause string, fwArgs []any) (ReportResult, error) {
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
		w := craftingWeight(artifactId, level)
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
			denomMap, err := denom1D(ctx, groupCol, def.NormalizeBy, baseWhere, baseArgs)
			if err != nil {
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

func executeWeightedPivot(ctx context.Context, def ReportDefinition, baseWhere string, args []any, baseArgs []any, fwClause string, fwArgs []any) (ReportResult, error) {
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

	accum := newPivotAccum()
	for rows.Next() {
		var rawRow, rawCol string
		var artifactId, level int64
		var capWeight float64
		if err := rows.Scan(&rawRow, &rawCol, &artifactId, &level, &capWeight); err != nil {
			return ReportResult{}, wrap(err)
		}
		w := craftingWeight(artifactId, level)
		rowLabel := FormatLabel(def.GroupBy, rawRow)
		colLabel := FormatLabel(def.SecondaryGroupBy, rawCol)
		accum.add(rowLabel, rawRow, colLabel, rawCol, capWeight*w)
	}
	if err := rows.Err(); err != nil {
		return ReportResult{}, wrap(err)
	}

	rowLabels, colLabels, rawRowLabels, rawColLabels, matrixValues :=
		accum.finalize(true, true, def.GroupBy, def.SecondaryGroupBy)

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
		util.Apply2DPctNormalization(matrixValues, len(rowLabels), len(colLabels), pctMode)
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

			denomMap, err := denom2D(ctx, def, col1, col2, pctMode, baseWhere, baseArgs)
			if err != nil {
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

func executeWeightedTimeSeries(ctx context.Context, def ReportDefinition, baseWhere string, args []any, fwClause string, fwArgs []any) (ReportResult, error) {
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
		w := craftingWeight(artifactId, level)
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

	buckets, floatValues = fillTimeSeriesGapsFloat(def.TimeBucket, def.CustomBucketUnit, buckets, floatValues)

	return ReportResult{
		Labels:      buckets,
		FloatValues: floatValues,
		IsFloat:     true,
		Weight:      def.Weight,
	}, nil
}

func executeWeightedTimePivot(ctx context.Context, def ReportDefinition, baseWhere string, args []any, fwClause string, fwArgs []any) (ReportResult, error) {
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

	accum := newPivotAccum()
	for rows.Next() {
		var rawBucket, rawGrp string
		var artifactId, level int64
		var capWeight float64
		if err := rows.Scan(&rawBucket, &rawGrp, &artifactId, &level, &capWeight); err != nil {
			return ReportResult{}, wrap(err)
		}
		w := craftingWeight(artifactId, level)
		grpLabel := FormatLabel(def.SecondaryGroupBy, rawGrp)
		// Bucket rows are not FormatLabel'd: the raw bucket string is the display.
		accum.add(rawBucket, rawBucket, grpLabel, rawGrp, capWeight*w)
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
