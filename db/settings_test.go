package db

import (
	"context"
	"database/sql"
	"path/filepath"
	"testing"
)

func setupTestDB(t *testing.T) {
	t.Helper()
	dbPath := filepath.Join(t.TempDir(), "test.db")
	if err := runMigrations(dbPath); err != nil {
		t.Fatalf("runMigrations: %v", err)
	}
	var err error
	_db, err = sql.Open("sqlite", dbPath+"?_pragma=foreign_keys(1)&_pragma=journal_mode(WAL)")
	if err != nil {
		t.Fatalf("sql.Open: %v", err)
	}
	_dbCtx, _dbCancel = context.WithCancel(context.Background())
	t.Cleanup(func() {
		_dbCancel()
		_db.Close()
		_db = nil
	})
}

func TestSetAndGetSetting(t *testing.T) {
	setupTestDB(t)
	ctx := context.Background()

	if err := SetSetting(ctx, "foo", "bar"); err != nil {
		t.Fatalf("SetSetting: %v", err)
	}
	all, err := GetAllSettings(ctx)
	if err != nil {
		t.Fatalf("GetAllSettings: %v", err)
	}
	if got := all["foo"]; got != "bar" {
		t.Errorf("got %q, want %q", got, "bar")
	}
}

func TestSetSettingOverwrite(t *testing.T) {
	setupTestDB(t)
	ctx := context.Background()

	if err := SetSetting(ctx, "key", "first"); err != nil {
		t.Fatalf("SetSetting first: %v", err)
	}
	if err := SetSetting(ctx, "key", "second"); err != nil {
		t.Fatalf("SetSetting second: %v", err)
	}
	all, err := GetAllSettings(ctx)
	if err != nil {
		t.Fatalf("GetAllSettings: %v", err)
	}
	if got := all["key"]; got != "second" {
		t.Errorf("got %q, want %q", got, "second")
	}
}

func TestSetSettingsBatch(t *testing.T) {
	setupTestDB(t)
	ctx := context.Background()

	batch := map[string]string{
		"a": "1",
		"b": "2",
		"c": "3",
	}
	if err := SetSettings(ctx, batch); err != nil {
		t.Fatalf("SetSettings: %v", err)
	}
	all, err := GetAllSettings(ctx)
	if err != nil {
		t.Fatalf("GetAllSettings: %v", err)
	}
	for k, want := range batch {
		if got := all[k]; got != want {
			t.Errorf("key %q: got %q, want %q", k, got, want)
		}
	}
}

func TestGetAllSettingsEmpty(t *testing.T) {
	setupTestDB(t)
	ctx := context.Background()

	all, err := GetAllSettings(ctx)
	if err != nil {
		t.Fatalf("GetAllSettings: %v", err)
	}
	if len(all) != 0 {
		t.Errorf("expected empty map, got %v", all)
	}
}
