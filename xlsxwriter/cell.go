package xlsxwriter

import (
	"strconv"
	"time"
)

// StyleID identifies a pre-defined number format style.
type StyleID int

const (
	StyleNone     StyleID = 0 // default, no number format
	StyleDatetime StyleID = 1 // yyyy-mm-dd hh:mm:ss
)

// Cell is a single spreadsheet value.
// Construct with StringCell, NumberCell, or DatetimeCell.
type Cell struct {
	strVal string
	numVal float64
	style  StyleID
	isNum  bool
}

// StringCell returns a plain text cell.
func StringCell(s string) Cell { return Cell{strVal: s} }

// NumberCell returns a plain numeric cell with no number format applied.
func NumberCell(v float64) Cell { return Cell{numVal: v, isNum: true} }

// DatetimeCell returns a numeric cell formatted as yyyy-mm-dd hh:mm:ss.
// t is converted to an Excel 1900 date serial (days since Dec 30, 1899 UTC).
func DatetimeCell(t time.Time) Cell {
	return Cell{numVal: excelSerial(t), style: StyleDatetime, isNum: true}
}

// cellRef converts 1-based (col, row) to a cell reference string like "A1".
func cellRef(col, row int) string {
	return colName(col) + strconv.Itoa(row)
}

// colName converts a 1-based column index to its letter string ("A", "B", ..., "Z", "AA", ...).
func colName(col int) string {
	name := ""
	for col > 0 {
		col--
		name = string(rune('A'+col%26)) + name
		col /= 26
	}
	return name
}

// excelSerial converts a time.Time to an Excel 1900 date serial number.
// Day 1 = Jan 1, 1900. Epoch offset is Dec 30, 1899 UTC.
func excelSerial(t time.Time) float64 {
	epoch := time.Date(1899, 12, 30, 0, 0, 0, 0, time.UTC)
	return t.UTC().Sub(epoch).Hours() / 24
}
