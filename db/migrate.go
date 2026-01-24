package db

import (
	"database/sql"
	"embed"
	"fmt"

	"github.com/golang-migrate/migrate/v4"
	sqlite3driver "github.com/golang-migrate/migrate/v4/database/sqlite3"
	_ "github.com/golang-migrate/migrate/v4/source/file"
	"github.com/golang-migrate/migrate/v4/source/iofs"
)

const _schemaVersion = 4

//go:embed migrations/*.sql
var _fs embed.FS

func runMigrations(dbPath string) (err error) {
	db, err := sql.Open("sqlite3", dbPath+"?_foreign_keys=on&_journal_mode=WAL")
	if err != nil {
		return fmt.Errorf("failed to open SQLite3 database %#v for migrations: %w", dbPath, err)
	}
	defer func() {
		closeErr := db.Close()
		if err == nil && closeErr != nil {
			err = fmt.Errorf("error closing database after migrations: %w", closeErr)
		}
	}()

	sourceDriver, err := iofs.New(_fs, "migrations")
	if err != nil {
		err = fmt.Errorf("failed to initialize migrations iofs driver: %w", err)
		return
	}
	databaseDriver, err := sqlite3driver.WithInstance(db, &sqlite3driver.Config{})
	if err != nil {
		err = fmt.Errorf("failed to initialize migrations sqlite3 driver: %w", err)
		return
	}
	m, err := migrate.NewWithInstance("iofs", sourceDriver, "sqlite3", databaseDriver)
	if err != nil {
		err = fmt.Errorf("failed to initialize schema migrator: %w", err)
		return
	}
	err = m.Migrate(_schemaVersion)
	if err != nil && err != migrate.ErrNoChange {
		err = fmt.Errorf("failed to migrate schemas in database %#v: %w", dbPath, err)
		return
	}
	err = nil
	return
}
