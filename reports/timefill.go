package reports

import (
	"fmt"
	"strconv"
	"strings"
	"time"
)

// fillTimeSeriesGaps inserts zero-value entries for every time bucket that
// falls between the first and last observed bucket but has no data, so that
// the chart X-axis is time-consistent rather than jumping over empty periods.
// Returns the inputs unchanged when the bucket type is unknown or the labels
// cannot be parsed.
func fillTimeSeriesGaps(timeBucket, customBucketUnit string, rawLabels []string, values []int64) ([]string, []int64) {
	if len(rawLabels) <= 1 {
		return rawLabels, values
	}
	allBuckets := expandBucketRange(effectiveBucketUnit(timeBucket, customBucketUnit), rawLabels[0], rawLabels[len(rawLabels)-1])
	if allBuckets == nil || len(allBuckets) == len(rawLabels) {
		return rawLabels, values
	}
	lookup := make(map[string]int64, len(rawLabels))
	for i, l := range rawLabels {
		lookup[l] = values[i]
	}
	out := make([]string, len(allBuckets))
	outV := make([]int64, len(allBuckets))
	for i, b := range allBuckets {
		out[i] = b
		outV[i] = lookup[b]
	}
	return out, outV
}

// fillTimeSeriesGapsFloat is the float variant used by weighted time series.
func fillTimeSeriesGapsFloat(timeBucket, customBucketUnit string, rawLabels []string, values []float64) ([]string, []float64) {
	if len(rawLabels) <= 1 {
		return rawLabels, values
	}
	allBuckets := expandBucketRange(effectiveBucketUnit(timeBucket, customBucketUnit), rawLabels[0], rawLabels[len(rawLabels)-1])
	if allBuckets == nil || len(allBuckets) == len(rawLabels) {
		return rawLabels, values
	}
	lookup := make(map[string]float64, len(rawLabels))
	for i, l := range rawLabels {
		lookup[l] = values[i]
	}
	out := make([]string, len(allBuckets))
	outV := make([]float64, len(allBuckets))
	for i, b := range allBuckets {
		out[i] = b
		outV[i] = lookup[b]
	}
	return out, outV
}

// fillTimePivotGaps inserts all-zero rows for missing time buckets in a 2-D
// (buckets × groups) matrix. nCols is the number of group columns.
// Returns the inputs unchanged when there are no gaps or the range cannot be parsed.
func fillTimePivotGaps(timeBucket, customBucketUnit string, bucketLabels []string, nCols int, matrixValues []float64) ([]string, []float64) {
	if len(bucketLabels) <= 1 || nCols == 0 {
		return bucketLabels, matrixValues
	}
	allBuckets := expandBucketRange(effectiveBucketUnit(timeBucket, customBucketUnit), bucketLabels[0], bucketLabels[len(bucketLabels)-1])
	if allBuckets == nil || len(allBuckets) == len(bucketLabels) {
		return bucketLabels, matrixValues
	}
	rowIndex := make(map[string]int, len(bucketLabels))
	for i, b := range bucketLabels {
		rowIndex[b] = i
	}
	newMatrix := make([]float64, len(allBuckets)*nCols)
	for newR, b := range allBuckets {
		if oldR, ok := rowIndex[b]; ok {
			copy(newMatrix[newR*nCols:(newR+1)*nCols], matrixValues[oldR*nCols:(oldR+1)*nCols])
		}
	}
	return allBuckets, newMatrix
}

func effectiveBucketUnit(timeBucket, customBucketUnit string) string {
	if timeBucket == "custom" {
		return customBucketUnit
	}
	return timeBucket
}

// expandBucketRange generates every time bucket label from first to last inclusive.
// Returns nil when the unit is unrecognised or the labels cannot be parsed.
func expandBucketRange(unit, first, last string) []string {
	switch unit {
	case "day":
		return expandDayBuckets(first, last)
	case "week":
		return expandWeekBuckets(first, last)
	case "month":
		return expandMonthBuckets(first, last)
	case "year":
		return expandYearBuckets(first, last)
	}
	return nil
}

func expandDayBuckets(first, last string) []string {
	t, err := time.Parse("2006-01-02", first)
	if err != nil {
		return nil
	}
	end, err := time.Parse("2006-01-02", last)
	if err != nil {
		return nil
	}
	var out []string
	for !t.After(end) {
		out = append(out, t.Format("2006-01-02"))
		t = t.AddDate(0, 0, 1)
	}
	return out
}

func expandWeekBuckets(first, last string) []string {
	start, err := parseSQLiteWeekLabel(first)
	if err != nil {
		return nil
	}
	end, err := parseSQLiteWeekLabel(last)
	if err != nil {
		return nil
	}
	var out []string
	for t := start; !t.After(end); t = t.AddDate(0, 0, 7) {
		out = append(out, formatSQLiteWeekLabel(t))
	}
	return out
}

func expandMonthBuckets(first, last string) []string {
	t, err := time.Parse("2006-01", first)
	if err != nil {
		return nil
	}
	end, err := time.Parse("2006-01", last)
	if err != nil {
		return nil
	}
	var out []string
	for !t.After(end) {
		out = append(out, t.Format("2006-01"))
		t = t.AddDate(0, 1, 0)
	}
	return out
}

func expandYearBuckets(first, last string) []string {
	y0, err := strconv.Atoi(first)
	if err != nil {
		return nil
	}
	y1, err := strconv.Atoi(last)
	if err != nil {
		return nil
	}
	out := make([]string, 0, y1-y0+1)
	for y := y0; y <= y1; y++ {
		out = append(out, fmt.Sprintf("%04d", y))
	}
	return out
}

// parseSQLiteWeekLabel parses a "YYYY-WW" string (SQLite %Y-%W) into the
// Monday that begins that week.
func parseSQLiteWeekLabel(label string) (time.Time, error) {
	parts := strings.SplitN(label, "-", 2)
	if len(parts) != 2 {
		return time.Time{}, fmt.Errorf("invalid week label %q", label)
	}
	year, err := strconv.Atoi(parts[0])
	if err != nil {
		return time.Time{}, err
	}
	week, err := strconv.Atoi(parts[1])
	if err != nil {
		return time.Time{}, err
	}
	return sqliteWeekStart(year, week), nil
}

// sqliteWeekStart returns the first day of the given SQLite %W week.
// SQLite week 0 contains all days before the year's first Monday;
// week 1 starts on the first Monday of the year.
func sqliteWeekStart(year, week int) time.Time {
	jan1 := time.Date(year, 1, 1, 0, 0, 0, 0, time.UTC)
	jan1MondayOffset := (int(jan1.Weekday()) + 6) % 7 // Monday=0 … Sunday=6
	dayOfYear := week*7 - jan1MondayOffset + 1
	if dayOfYear < 1 {
		dayOfYear = 1
	}
	return jan1.AddDate(0, 0, dayOfYear-1)
}

// formatSQLiteWeekLabel formats t as "YYYY-WW" matching SQLite's strftime('%Y-%W', ...).
func formatSQLiteWeekLabel(t time.Time) string {
	jan1 := time.Date(t.Year(), 1, 1, 0, 0, 0, 0, time.UTC)
	jan1MondayOffset := (int(jan1.Weekday()) + 6) % 7
	week := (t.YearDay() - 1 + jan1MondayOffset) / 7
	return fmt.Sprintf("%04d-%02d", t.Year(), week)
}
