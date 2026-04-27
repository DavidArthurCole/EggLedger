package main

import (
	"os"
	"path/filepath"
	"testing"
)

func writeTestExportFile(t *testing.T, dir, name string, size int) {
	t.Helper()
	data := make([]byte, size)
	if err := os.WriteFile(filepath.Join(dir, name), data, 0644); err != nil {
		t.Fatal(err)
	}
}

func TestListExportGroups_MissingDir(t *testing.T) {
	groups, err := listExportGroups(t.TempDir())
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if len(groups) != 0 {
		t.Fatalf("expected empty, got %d groups", len(groups))
	}
}

func TestListExportGroups_ParsesAndGroups(t *testing.T) {
	root := t.TempDir()
	md := filepath.Join(root, "missions")
	os.MkdirAll(md, 0755)

	writeTestExportFile(t, md, "EI123.20240311_221737.csv", 100)
	writeTestExportFile(t, md, "EI123.20240311_221737.xlsx", 200)
	writeTestExportFile(t, md, "EI123.20240312_100000.csv", 150)
	writeTestExportFile(t, md, "EI456.20240311_221737.csv", 300)
	writeTestExportFile(t, md, "ignored.txt", 10)

	groups, err := listExportGroups(root)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if len(groups) != 2 {
		t.Fatalf("expected 2 EID groups, got %d", len(groups))
	}

	var ei123 ExportGroup
	for _, g := range groups {
		if g.Eid == "EI123" {
			ei123 = g
		}
	}
	if len(ei123.Pairs) != 2 {
		t.Fatalf("expected 2 pairs for EI123, got %d", len(ei123.Pairs))
	}
	if ei123.Pairs[0].Timestamp != "20240312_100000" {
		t.Errorf("expected newest pair first, got %s", ei123.Pairs[0].Timestamp)
	}
	if ei123.Pairs[1].CsvSize != 100 || ei123.Pairs[1].XlsxSize != 200 {
		t.Errorf("unexpected file sizes: csv=%d xlsx=%d", ei123.Pairs[1].CsvSize, ei123.Pairs[1].XlsxSize)
	}
}

func TestListExportGroups_PartialPair(t *testing.T) {
	root := t.TempDir()
	md := filepath.Join(root, "missions")
	os.MkdirAll(md, 0755)
	writeTestExportFile(t, md, "EI123.20240311_221737.csv", 50) // xlsx missing

	groups, err := listExportGroups(root)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if len(groups) != 1 || len(groups[0].Pairs) != 1 {
		t.Fatalf("expected 1 group with 1 pair")
	}
	if groups[0].Pairs[0].XlsxPath != "" {
		t.Error("expected empty XlsxPath for missing xlsx")
	}
}

func TestPruneExportsForPlayer_DeletesOldest(t *testing.T) {
	root := t.TempDir()
	md := filepath.Join(root, "missions")
	os.MkdirAll(md, 0755)

	for _, f := range []string{
		"EI123.20240310_000000.csv", "EI123.20240310_000000.xlsx",
		"EI123.20240311_000000.csv", "EI123.20240311_000000.xlsx",
		"EI123.20240312_000000.csv", "EI123.20240312_000000.xlsx",
	} {
		writeTestExportFile(t, md, f, 4)
	}

	deleted, freed, err := pruneExportsForPlayer(root, "EI123", 2)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if deleted != 2 {
		t.Errorf("expected 2 files deleted, got %d", deleted)
	}
	if freed != 8 {
		t.Errorf("expected 8 bytes freed, got %d", freed)
	}
	// oldest pair gone
	if _, err := os.Stat(filepath.Join(md, "EI123.20240310_000000.csv")); !os.IsNotExist(err) {
		t.Error("oldest csv should be deleted")
	}
	// newest pair present
	if _, err := os.Stat(filepath.Join(md, "EI123.20240312_000000.csv")); err != nil {
		t.Error("newest csv should remain")
	}
}

func TestPruneExportsForPlayer_NoOp(t *testing.T) {
	root := t.TempDir()
	deleted, freed, err := pruneExportsForPlayer(root, "EI123", 0)
	if err != nil || deleted != 0 || freed != 0 {
		t.Errorf("keepCount=0 should be no-op, got deleted=%d freed=%d err=%v", deleted, freed, err)
	}
}

func TestPruneExportsForPlayer_BelowLimit(t *testing.T) {
	root := t.TempDir()
	md := filepath.Join(root, "missions")
	os.MkdirAll(md, 0755)
	writeTestExportFile(t, md, "EI123.20240311_221737.csv", 10)

	deleted, _, err := pruneExportsForPlayer(root, "EI123", 5)
	if err != nil || deleted != 0 {
		t.Errorf("below limit should delete nothing, got deleted=%d err=%v", deleted, err)
	}
}
