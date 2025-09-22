package db

import (
	"database/sql"
	"embed"

	"github.com/golang-migrate/migrate/v4"
	sqlite3driver "github.com/golang-migrate/migrate/v4/database/sqlite3"
	_ "github.com/golang-migrate/migrate/v4/source/file"
	"github.com/golang-migrate/migrate/v4/source/iofs"
	"github.com/pkg/errors"
)

const _schemaVersion = 4

//go:embed migrations/*.sql
var _fs embed.FS

func runMigrations(dbPath string) (err error) {
	db, err := sql.Open("sqlite3", dbPath+"?_foreign_keys=on&_journal_mode=WAL")
	if err != nil {
		return errors.Wrapf(err, "failed to open SQLite3 database %#v for migrations", dbPath)
	}
	defer func() {
		closeErr := db.Close()
		if err == nil && closeErr != nil {
			err = errors.Wrap(closeErr, "error closing database after migrations")
		}
	}()

	sourceDriver, err := iofs.New(_fs, "migrations")
	if err != nil {
		err = errors.Wrap(err, "failed to initialize migrations iofs driver")
		return
	}
	databaseDriver, err := sqlite3driver.WithInstance(db, &sqlite3driver.Config{})
	if err != nil {
		err = errors.Wrap(err, "failed to initialize migrations sqlite3 driver")
		return
	}
	m, err := migrate.NewWithInstance("iofs", sourceDriver, "sqlite3", databaseDriver)
	if err != nil {
		err = errors.Wrap(err, "failed to initialize schema migrator")
		return
	}
	err = m.Migrate(_schemaVersion)
	if err != nil && err != migrate.ErrNoChange {
		err = errors.Wrapf(err, "failed to migrate schemas in database %#v", dbPath)
		return
	}
	err = nil
	return
}
