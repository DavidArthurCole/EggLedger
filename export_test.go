package main

import (
	"archive/zip"
	"encoding/csv"
	"io"
	"os"
	"path/filepath"
	"strings"
	"testing"
	"time"

	"github.com/DavidArthurCole/EggLedger/ei"
)

func testMissions() []*mission {
	return []*mission{
		{
			Id:               "test-uuid-001",
			TypeName:         "Standard",
			ShipName:         "Chicken One",
			DurationTypeName: "Short",
			Level:            1,
			LaunchedAt:       time.Date(2023, 1, 1, 12, 0, 0, 0, time.UTC),
			LaunchedAtStr:    "2023-01-01T12:00:00Z",
			ReturnedAt:       time.Date(2023, 1, 1, 14, 0, 0, 0, time.UTC),
			ReturnedAtStr:    "2023-01-01T14:00:00Z",
			DurationDays:     2.0 / 24.0,
			Capacity:         50,
			TargetArtifact:   ei.ArtifactSpec_UNKNOWN,
			ArtifactNames:    []string{"Book of Basan (T4)", "Lunar Totem (T1)"},
		},
		{
			Id:               "test-uuid-002",
			TypeName:         "Standard",
			ShipName:         "Chicken Nine",
			DurationTypeName: "Epic",
			Level:            8,
			LaunchedAt:       time.Date(2023, 6, 15, 0, 0, 0, 0, time.UTC),
			LaunchedAtStr:    "2023-06-15T00:00:00Z",
			ReturnedAt:       time.Date(2023, 6, 22, 0, 0, 0, 0, time.UTC),
			ReturnedAtStr:    "2023-06-22T00:00:00Z",
			DurationDays:     7.0,
			Capacity:         400,
			TargetArtifact:   ei.ArtifactSpec_BOOK_OF_BASAN,
			ArtifactNames:    []string{"Interstellar Compass (T4)"},
		},
	}
}

func TestExportMissionsToCsv_structure(t *testing.T) {
	missions := testMissions()
	dir := t.TempDir()
	path := filepath.Join(dir, "test.csv")

	if err := exportMissionsToCsv(missions, path); err != nil {
		t.Fatal(err)
	}

	f, err := os.Open(path)
	if err != nil {
		t.Fatal(err)
	}
	defer f.Close()

	records, err := csv.NewReader(f).ReadAll()
	if err != nil {
		t.Fatal(err)
	}
	if len(records) != 3 {
		t.Fatalf("expected 3 rows (header + 2 missions), got %d", len(records))
	}

	header := records[0]
	// 10 fixed columns + 2 artifact columns (max across both missions)
	if len(header) != 12 {
		t.Fatalf("expected 12 columns, got %d: %v", len(header), header)
	}
	wantHeaders := []string{
		"ID", "Type", "Ship", "Duration Type", "Level",
		"Launched at", "Returned at", "Duration days", "Capacity", "Target",
		"Artifact 1", "Artifact 2",
	}
	for i, want := range wantHeaders {
		if header[i] != want {
			t.Errorf("header[%d]: want %q, got %q", i, want, header[i])
		}
	}

	row1 := records[1]
	if row1[0] != "test-uuid-001" {
		t.Errorf("row1 ID: want test-uuid-001, got %s", row1[0])
	}
	if row1[10] != "Book of Basan (T4)" {
		t.Errorf("row1 Artifact 1: want %q, got %q", "Book of Basan (T4)", row1[10])
	}
	if row1[11] != "Lunar Totem (T1)" {
		t.Errorf("row1 Artifact 2: want %q, got %q", "Lunar Totem (T1)", row1[11])
	}

	// Mission 2 has only 1 artifact - second artifact column must be empty string.
	row2 := records[2]
	if row2[11] != "" {
		t.Errorf("row2 Artifact 2: want empty string, got %q", row2[11])
	}
}

func TestMissionTypeName_Unknown(t *testing.T) {
	cases := []struct {
		input int
		want  string
	}{
		{0, "Standard"},
		{1, "Virtue"},
		{-1, "Unknown"},
		{99, "Unknown"},
	}
	for _, tc := range cases {
		got := missionTypeName(tc.input)
		if got != tc.want {
			t.Errorf("missionTypeName(%d) = %q, want %q", tc.input, got, tc.want)
		}
	}
}

func TestExportMissionsToCsv_UnknownMissionType(t *testing.T) {
	missions := []*mission{
		{
			Id:               "test-unknown-type",
			TypeName:         missionTypeName(-1),
			ShipName:         "Chicken One",
			DurationTypeName: "Short",
			Level:            0,
			LaunchedAt:       time.Date(2024, 1, 1, 0, 0, 0, 0, time.UTC),
			LaunchedAtStr:    "2024-01-01T00:00:00Z",
			ReturnedAt:       time.Date(2024, 1, 1, 1, 0, 0, 0, time.UTC),
			ReturnedAtStr:    "2024-01-01T01:00:00Z",
			DurationDays:     1.0 / 24.0,
			Capacity:         6,
			TargetArtifact:   ei.ArtifactSpec_UNKNOWN,
			ArtifactNames:    []string{},
		},
	}

	dir := t.TempDir()
	path := filepath.Join(dir, "unknown-type.csv")

	if err := exportMissionsToCsv(missions, path); err != nil {
		t.Fatal(err)
	}

	f, err := os.Open(path)
	if err != nil {
		t.Fatal(err)
	}
	defer f.Close()

	records, err := csv.NewReader(f).ReadAll()
	if err != nil {
		t.Fatal(err)
	}
	if len(records) != 2 {
		t.Fatalf("expected 2 rows (header + 1 mission), got %d", len(records))
	}

	typeCol := records[1][1]
	if typeCol != "Unknown" {
		t.Errorf("Type column: want %q, got %q", "Unknown", typeCol)
	}
}

func TestExportMissionsToXlsx_structure(t *testing.T) {
	missions := testMissions()
	dir := t.TempDir()
	path := filepath.Join(dir, "test.xlsx")

	if err := exportMissionsToXlsx(missions, path); err != nil {
		t.Fatal(err)
	}

	zr, err := zip.OpenReader(path)
	if err != nil {
		t.Fatalf("result is not a valid ZIP/XLSX: %v", err)
	}
	defer zr.Close()

	var sheetXML string
	for _, f := range zr.File {
		if f.Name == "xl/worksheets/sheet1.xml" {
			rc, _ := f.Open()
			b, _ := io.ReadAll(rc)
			rc.Close()
			sheetXML = string(b)
		}
	}
	if sheetXML == "" {
		t.Fatal("xl/worksheets/sheet1.xml not found in XLSX output")
	}

	// All 10 fixed headers must be present as inline string cells.
	wantHeaders := []string{
		"ID", "Type", "Ship", "Duration Type", "Level",
		"Launched at", "Returned at", "Duration days", "Capacity", "Target",
	}
	for _, h := range wantHeaders {
		if !strings.Contains(sheetXML, "<t>"+h+"</t>") {
			t.Errorf("sheet XML missing header %q", h)
		}
	}

	// Two missions, max 2 artifacts -> "Artifact 2" header must be present.
	if !strings.Contains(sheetXML, "<t>Artifact 2</t>") {
		t.Error("sheet XML missing \"Artifact 2\" column header")
	}

	// Datetime cells must carry s="1" (StyleDatetime).
	if !strings.Contains(sheetXML, `s="1"`) {
		t.Error("sheet XML missing datetime style s=\"1\" on launched-at/returned-at cells")
	}

	// Mission IDs must appear as inline string cells.
	if !strings.Contains(sheetXML, "test-uuid-001") {
		t.Error("sheet XML missing mission ID \"test-uuid-001\"")
	}
}
