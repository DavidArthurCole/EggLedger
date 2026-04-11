// Package xlsxwriter writes minimal single-sheet XLSX files.
// It supports streaming row writes, optional column widths, and a single
// built-in datetime number format style. No reading capability.
package xlsxwriter

import (
	"archive/zip"
	"bytes"
	"encoding/xml"
	"fmt"
	"io"
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

// Writer streams a single-sheet XLSX file to an io.Writer.
// Call SetColWidths before the first WriteRow.
// Call Close to finalise the ZIP archive.
type Writer struct {
	zw           *zip.Writer
	sheetW       io.Writer
	colWidths    []float64
	rowNum       int
	sheetStarted bool
}

// New creates a Writer that writes to w.
// Static ZIP entries (content types, relationships, workbook, styles) are written immediately.
func New(w io.Writer) (*Writer, error) {
	zw := zip.NewWriter(w)
	wtr := &Writer{zw: zw}
	if err := wtr.writeStaticEntries(); err != nil {
		return nil, err
	}
	return wtr, nil
}

// SetColWidths sets column widths in Excel character units (index 0 = column A).
// Must be called before the first WriteRow.
func (w *Writer) SetColWidths(widths []float64) {
	w.colWidths = widths
}

// WriteRow appends a row of cells to the sheet.
func (w *Writer) WriteRow(cells []Cell) error {
	if !w.sheetStarted {
		if err := w.startSheet(); err != nil {
			return err
		}
		w.sheetStarted = true
	}
	w.rowNum++
	var buf bytes.Buffer
	fmt.Fprintf(&buf, `<row r="%d">`, w.rowNum)
	for col, cell := range cells {
		ref := cellRef(col+1, w.rowNum)
		if cell.isNum {
			s := strconv.FormatFloat(cell.numVal, 'f', -1, 64)
			if cell.style != StyleNone {
				fmt.Fprintf(&buf, `<c r="%s" s="%d"><v>%s</v></c>`, ref, int(cell.style), s)
			} else {
				fmt.Fprintf(&buf, `<c r="%s"><v>%s</v></c>`, ref, s)
			}
		} else {
			var escaped bytes.Buffer
			if err := xml.EscapeText(&escaped, []byte(cell.strVal)); err != nil {
				return err
			}
			fmt.Fprintf(&buf, `<c r="%s" t="inlineStr"><is><t>%s</t></is></c>`, ref, escaped.String())
		}
	}
	buf.WriteString("</row>")
	_, err := w.sheetW.Write(buf.Bytes())
	return err
}

// Close finalises the sheet XML and closes the ZIP archive.
func (w *Writer) Close() error {
	if !w.sheetStarted {
		if err := w.startSheet(); err != nil {
			_ = w.zw.Close()
			return err
		}
	}
	var firstErr error
	if _, err := io.WriteString(w.sheetW, `</sheetData></worksheet>`); err != nil {
		firstErr = err
	}
	if err := w.zw.Close(); err != nil && firstErr == nil {
		firstErr = err
	}
	return firstErr
}

func (w *Writer) startSheet() error {
	fh := &zip.FileHeader{Name: "xl/worksheets/sheet1.xml", Method: zip.Deflate}
	sw, err := w.zw.CreateHeader(fh)
	if err != nil {
		return err
	}
	w.sheetW = sw
	if _, err := io.WriteString(sw,
		`<?xml version="1.0" encoding="UTF-8" standalone="yes"?>`+
			`<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">`+
			`<sheetFormatPr defaultRowHeight="15"/>`,
	); err != nil {
		return err
	}
	if len(w.colWidths) > 0 {
		if _, err := io.WriteString(sw, "<cols>"); err != nil {
			return err
		}
		for i, width := range w.colWidths {
			col := i + 1
			if _, err := fmt.Fprintf(sw, `<col min="%d" max="%d" width="%.2f" customWidth="1"/>`, col, col, width); err != nil {
				return err
			}
		}
		if _, err := io.WriteString(sw, "</cols>"); err != nil {
			return err
		}
	}
	_, err = io.WriteString(sw, "<sheetData>")
	return err
}

func (w *Writer) writeStaticEntries() error {
	for name, content := range staticEntries {
		fw, err := w.zw.CreateHeader(&zip.FileHeader{Name: name, Method: zip.Deflate})
		if err != nil {
			return err
		}
		if _, err := io.WriteString(fw, content); err != nil {
			return err
		}
	}
	return nil
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

// staticEntries contains all ZIP entries whose content does not depend on row data.
var staticEntries = map[string]string{
	"[Content_Types].xml": `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>` +
		`<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">` +
		`<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>` +
		`<Default Extension="xml" ContentType="application/xml"/>` +
		`<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>` +
		`<Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>` +
		`<Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>` +
		`</Types>`,
	"_rels/.rels": `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>` +
		`<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">` +
		`<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>` +
		`</Relationships>`,
	"xl/workbook.xml": `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>` +
		`<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">` +
		`<sheets><sheet name="Sheet1" sheetId="1" r:id="rId1"/></sheets>` +
		`</workbook>`,
	"xl/_rels/workbook.xml.rels": `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>` +
		`<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">` +
		`<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>` +
		`<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>` +
		`</Relationships>`,
	"xl/styles.xml": `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>` +
		`<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">` +
		`<numFmts count="1"><numFmt numFmtId="164" formatCode="yyyy-mm-dd hh:mm:ss"/></numFmts>` +
		`<fonts count="1"><font><name val="Consolas"/><sz val="11"/></font></fonts>` +
		`<fills count="2">` +
		`<fill><patternFill patternType="none"/></fill>` +
		`<fill><patternFill patternType="gray125"/></fill>` +
		`</fills>` +
		`<borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>` +
		`<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>` +
		`<cellXfs count="2">` +
		`<xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>` +
		`<xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>` +
		`</cellXfs>` +
		`<cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>` +
		`</styleSheet>`,
}
