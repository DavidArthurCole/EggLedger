package reports

import "sort"

// labelEntry pairs a display label with the raw SQL value it was derived from.
// The raw value drives sort order (via LabelSortLess); the display value is what
// is surfaced to the UI and used as the cells map key.
type labelEntry struct {
	display string
	rawVal  string
}

// pivotAxis tracks the dedup-on-first-sight set of labels for one axis of a pivot
// (rows or cols), preserving insertion order in entries.
type pivotAxis struct {
	seen    map[string]struct{}
	entries []labelEntry
}

// add records (display, rawVal) the first time the display label is seen,
// preserving the order in which labels first appear.
func (a *pivotAxis) add(display, rawVal string) {
	if a.seen == nil {
		a.seen = map[string]struct{}{}
	}
	if _, ok := a.seen[display]; ok {
		return
	}
	a.seen[display] = struct{}{}
	a.entries = append(a.entries, labelEntry{display: display, rawVal: rawVal})
}

// pivotAccum owns the shared 2D pivot collection machinery: row/col label sets,
// accumulated cell values keyed by DISPLAY label, and the sort + flatten step.
// Cells are always accumulated with +=; callers whose query guarantees one row
// per (row, col) pair (GROUP BY) get identical results to a plain assignment.
type pivotAccum struct {
	rows  pivotAxis
	cols  pivotAxis
	cells map[string]map[string]float64
}

func newPivotAccum() *pivotAccum {
	return &pivotAccum{cells: map[string]map[string]float64{}}
}

// add registers a row label, a col label, and accumulates val into the cell at
// their intersection (keyed by display labels).
func (p *pivotAccum) add(rowDisplay, rowRaw, colDisplay, colRaw string, val float64) {
	p.rows.add(rowDisplay, rowRaw)
	p.cols.add(colDisplay, colRaw)
	if p.cells[rowDisplay] == nil {
		p.cells[rowDisplay] = map[string]float64{}
	}
	p.cells[rowDisplay][colDisplay] += val
}

// finalize sorts the requested axes (rows via rowGroupBy, cols via colGroupBy,
// both by LabelSortLess on their raw values) and flattens the cells into a
// row-major matrix indexed r*nC+c. It returns the display label slices, the raw
// label slices (parallel to the display slices), and the matrix.
//
// When sortRows is false the row axis stays in insertion order (used by the time
// pivots, whose buckets must remain in query ASC order). Cols are sorted whenever
// sortCols is true.
func (p *pivotAccum) finalize(sortRows, sortCols bool, rowGroupBy, colGroupBy string) (
	rowLabels, colLabels, rawRowLabels, rawColLabels []string, matrix []float64,
) {
	rowEntries := p.rows.entries
	colEntries := p.cols.entries

	if sortRows {
		sort.SliceStable(rowEntries, func(i, j int) bool {
			return LabelSortLess(rowGroupBy, rowEntries[i].rawVal, rowEntries[j].rawVal)
		})
	}
	if sortCols {
		sort.SliceStable(colEntries, func(i, j int) bool {
			return LabelSortLess(colGroupBy, colEntries[i].rawVal, colEntries[j].rawVal)
		})
	}

	nR := len(rowEntries)
	nC := len(colEntries)
	rowLabels = make([]string, nR)
	rawRowLabels = make([]string, nR)
	for i, e := range rowEntries {
		rowLabels[i] = e.display
		rawRowLabels[i] = e.rawVal
	}
	colLabels = make([]string, nC)
	rawColLabels = make([]string, nC)
	for i, e := range colEntries {
		colLabels[i] = e.display
		rawColLabels[i] = e.rawVal
	}

	matrix = make([]float64, nR*nC)
	for r, row := range rowLabels {
		cellRow := p.cells[row]
		if cellRow == nil {
			continue
		}
		for c, col := range colLabels {
			matrix[r*nC+c] = cellRow[col]
		}
	}
	return rowLabels, colLabels, rawRowLabels, rawColLabels, matrix
}
