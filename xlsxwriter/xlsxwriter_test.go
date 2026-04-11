package xlsxwriter_test

import (
	"archive/zip"
	"bytes"
	"io"
	"strings"
	"testing"
	"time"

	"github.com/DavidArthurCole/EggLedger/xlsxwriter"
)

// buildXlsx writes a two-row XLSX (header + one data row) and returns the raw bytes.
func buildXlsx(t *testing.T) []byte {
	t.Helper()
	var buf bytes.Buffer
	w, err := xlsxwriter.New(&buf)
	if err != nil {
		t.Fatal(err)
	}
	w.SetColWidths([]float64{20.0, 22.0, 15.0})
	if err := w.WriteRow([]xlsxwriter.Cell{
		xlsxwriter.StringCell("ID"),
		xlsxwriter.StringCell("Launched at"),
		xlsxwriter.StringCell("Duration days"),
	}); err != nil {
		t.Fatal(err)
	}
	ts := time.Date(2023, 5, 15, 14, 30, 0, 0, time.UTC)
	if err := w.WriteRow([]xlsxwriter.Cell{
		xlsxwriter.StringCell("abc<>&123"),
		xlsxwriter.DatetimeCell(ts),
		xlsxwriter.NumberCell(0.25),
	}); err != nil {
		t.Fatal(err)
	}
	if err := w.Close(); err != nil {
		t.Fatal(err)
	}
	return buf.Bytes()
}

// openXlsx reads raw XLSX bytes and returns the ZIP reader and a map of entry name -> content.
func openXlsx(t *testing.T, data []byte) (*zip.Reader, map[string]string) {
	t.Helper()
	zr, err := zip.NewReader(bytes.NewReader(data), int64(len(data)))
	if err != nil {
		t.Fatalf("result is not a valid ZIP: %v", err)
	}
	contents := make(map[string]string)
	for _, f := range zr.File {
		rc, err := f.Open()
		if err != nil {
			t.Fatal(err)
		}
		b, err := io.ReadAll(rc)
		rc.Close()
		if err != nil {
			t.Fatal(err)
		}
		contents[f.Name] = string(b)
	}
	return zr, contents
}

func TestWriter_zipEntries(t *testing.T) {
	_, contents := openXlsx(t, buildXlsx(t))
	required := []string{
		"[Content_Types].xml",
		"_rels/.rels",
		"xl/workbook.xml",
		"xl/_rels/workbook.xml.rels",
		"xl/styles.xml",
		"xl/worksheets/sheet1.xml",
	}
	for _, name := range required {
		if _, ok := contents[name]; !ok {
			t.Errorf("missing required ZIP entry: %s", name)
		}
	}
}

func TestWriter_sheetStructure(t *testing.T) {
	_, contents := openXlsx(t, buildXlsx(t))
	sheet := contents["xl/worksheets/sheet1.xml"]
	checks := []struct {
		desc string
		want string
	}{
		{"cols section open tag", `<cols>`},
		{"column A width", `width="20.00"`},
		{"sheetData open tag", `<sheetData>`},
		{"row 1", `<row r="1">`},
		{"row 2", `<row r="2">`},
		{"cell A1", `r="A1"`},
		{"cell B2", `r="B2"`},
	}
	for _, c := range checks {
		if !strings.Contains(sheet, c.want) {
			t.Errorf("%s: sheet XML does not contain %q", c.desc, c.want)
		}
	}
}

func TestWriter_stringCell(t *testing.T) {
	_, contents := openXlsx(t, buildXlsx(t))
	sheet := contents["xl/worksheets/sheet1.xml"]
	if !strings.Contains(sheet, `t="inlineStr"`) {
		t.Error("string cells must use t=\"inlineStr\"")
	}
	if !strings.Contains(sheet, "<t>ID</t>") {
		t.Error("sheet missing header text \"ID\"")
	}
}

func TestWriter_xmlEscaping(t *testing.T) {
	_, contents := openXlsx(t, buildXlsx(t))
	sheet := contents["xl/worksheets/sheet1.xml"]
	if strings.Contains(sheet, "abc<>&123") {
		t.Error("string cell value was not XML-escaped")
	}
	if !strings.Contains(sheet, "abc&lt;&gt;&amp;123") {
		t.Errorf("string cell missing expected escaping of <>&; sheet:\n%s", sheet)
	}
}

func TestWriter_datetimeCell(t *testing.T) {
	_, contents := openXlsx(t, buildXlsx(t))
	sheet := contents["xl/worksheets/sheet1.xml"]
	// Datetime cell must carry style s="1" (StyleDatetime).
	if !strings.Contains(sheet, `s="1"`) {
		t.Error("datetime cell missing s=\"1\" style attribute")
	}
	// 2023-05-15 14:30:00 UTC -> Excel serial starts with 45061.
	if !strings.Contains(sheet, "45061") {
		t.Errorf("datetime serial should start with 45061; sheet:\n%s", sheet)
	}
}

func TestWriter_numberCell(t *testing.T) {
	_, contents := openXlsx(t, buildXlsx(t))
	sheet := contents["xl/worksheets/sheet1.xml"]
	if !strings.Contains(sheet, "<v>0.25</v>") {
		t.Errorf("plain number cell missing <v>0.25</v>; sheet:\n%s", sheet)
	}
}

func TestWriter_styles(t *testing.T) {
	_, contents := openXlsx(t, buildXlsx(t))
	styles := contents["xl/styles.xml"]
	if !strings.Contains(styles, "Consolas") {
		t.Error("styles.xml missing Consolas font")
	}
	if !strings.Contains(styles, "yyyy-mm-dd hh:mm:ss") {
		t.Error("styles.xml missing correct datetime format code")
	}
}

func TestWriter_emptySheet(t *testing.T) {
	// Close with no rows written must still produce a valid ZIP.
	var buf bytes.Buffer
	w, err := xlsxwriter.New(&buf)
	if err != nil {
		t.Fatal(err)
	}
	if err := w.Close(); err != nil {
		t.Fatal(err)
	}
	if _, err := zip.NewReader(bytes.NewReader(buf.Bytes()), int64(buf.Len())); err != nil {
		t.Fatalf("empty-sheet result is not a valid ZIP: %v", err)
	}
}
