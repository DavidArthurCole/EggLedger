package reports

import (
	"context"
	"fmt"
)

// denom1D runs the per-group denominator query for "launches" (COUNT) or "airtime"
// (SUM of flight hours) normalization, keyed by the raw group value.
func denom1D(ctx context.Context, groupCol, mode, baseWhere string, baseArgs []any) (map[string]float64, error) {
	var denomQuery string
	if mode == "airtime" {
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
		return nil, err
	}
	defer denomRows.Close()
	denomMap := make(map[string]float64)
	for denomRows.Next() {
		var key string
		var denom float64
		if err := denomRows.Scan(&key, &denom); err != nil {
			return nil, err
		}
		denomMap[key] = denom
	}
	if err := denomRows.Err(); err != nil {
		return nil, err
	}
	return denomMap, nil
}

// denom2D runs the per-cell denominator query for a 2D pivot, keyed by [rawRow][rawCol]
// using FormatLabel-resolved keys to match the cell maps.
func denom2D(ctx context.Context, def ReportDefinition, col1, col2, mode, baseWhere string, baseArgs []any) (map[string]map[string]float64, error) {
	var denomQuery string
	if mode == "airtime" {
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
		return nil, err
	}
	defer denomRows.Close()
	denomMap := map[string]map[string]float64{}
	for denomRows.Next() {
		var rawKey1, rawKey2 string
		var denom float64
		if err := denomRows.Scan(&rawKey1, &rawKey2, &denom); err != nil {
			return nil, err
		}
		k1 := FormatLabel(def.GroupBy, rawKey1)
		k2 := FormatLabel(def.SecondaryGroupBy, rawKey2)
		if denomMap[k1] == nil {
			denomMap[k1] = map[string]float64{}
		}
		denomMap[k1][k2] = denom
	}
	if err := denomRows.Err(); err != nil {
		return nil, err
	}
	return denomMap, nil
}
