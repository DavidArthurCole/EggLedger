package reportdb_test

import (
	"context"
	"os"
	"path/filepath"
	"testing"

	"github.com/DavidArthurCole/EggLedger/reportdb"
)

func TestMain(m *testing.M) {
	dir, err := os.MkdirTemp("", "reportdb_test_*")
	if err != nil {
		panic(err)
	}
	defer os.RemoveAll(dir)

	path := filepath.Join(dir, "test_reports.db")
	if err := reportdb.InitReportDB(path); err != nil {
		panic(err)
	}
	defer reportdb.CloseReportDB()

	os.Exit(m.Run())
}

func TestInsertAndRetrieve(t *testing.T) {
	r := reportdb.ReportRow{
		Id: "test-1", AccountId: "EI1234", Name: "Test Report",
		Subject: "ships", Mode: "aggregate", DisplayMode: "counts",
		GroupBy: "ship_type", FiltersJSON: `{"and":[],"or":[]}`,
		GridX: 0, GridY: 0, GridW: 2, GridH: 1,
		Weight: "LOW", SortOrder: 0,
	}
	if err := reportdb.InsertReport(context.Background(), r); err != nil {
		t.Fatalf("InsertReport: %v", err)
	}
	got, err := reportdb.RetrieveReport(context.Background(), "test-1")
	if err != nil {
		t.Fatalf("RetrieveReport: %v", err)
	}
	if got.Name != "Test Report" {
		t.Errorf("want Name=%q got %q", "Test Report", got.Name)
	}
}

func TestReorderReports(t *testing.T) {
	t.Cleanup(func() {
		reportdb.DeleteReport(context.Background(), "a")
		reportdb.DeleteReport(context.Background(), "b")
		reportdb.DeleteReport(context.Background(), "c")
	})
	for i, id := range []string{"a", "b", "c"} {
		r := reportdb.ReportRow{
			Id: id, AccountId: "EI1", Name: id,
			Subject: "ships", Mode: "aggregate", DisplayMode: "counts",
			GroupBy: "ship_type", FiltersJSON: `{"and":[],"or":[]}`,
			GridX: 0, GridY: 0, GridW: 1, GridH: 1,
			Weight: "LOW", SortOrder: i,
		}
		if err := reportdb.InsertReport(context.Background(), r); err != nil {
			t.Fatalf("InsertReport %s: %v", id, err)
		}
	}
	if err := reportdb.ReorderReports(context.Background(), []string{"c", "a", "b"}); err != nil {
		t.Fatalf("ReorderReports: %v", err)
	}
	rows, _ := reportdb.RetrieveAccountReports(context.Background(), "EI1")
	if rows[0].Id != "c" {
		t.Errorf("expected first to be 'c', got %q", rows[0].Id)
	}
}
