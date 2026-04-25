package reportdb

import (
	"database/sql"
	"embed"

	"github.com/golang-migrate/migrate/v4"
	sqlitedriver "github.com/golang-migrate/migrate/v4/database/sqlite"
	"github.com/golang-migrate/migrate/v4/source/iofs"
	"github.com/pkg/errors"
	_ "modernc.org/sqlite"
)

const _schemaVersion = 7

//go:embed migrations/*.sql
var _fs embed.FS

func runMigrations(dbPath string) (err error) {
	db, err := sql.Open("sqlite", dbPath+"?_pragma=foreign_keys(1)&_pragma=journal_mode(WAL)")
	if err != nil {
		return errors.Wrapf(err, "failed to open report DB %#v for migrations", dbPath)
	}
	defer func() {
		closeErr := db.Close()
		if err == nil && closeErr != nil {
			err = errors.Wrap(closeErr, "error closing report DB after migrations")
		}
	}()

	sourceDriver, err := iofs.New(_fs, "migrations")
	if err != nil {
		err = errors.Wrap(err, "failed to initialize report DB migrations iofs driver")
		return
	}
	databaseDriver, err := sqlitedriver.WithInstance(db, &sqlitedriver.Config{})
	if err != nil {
		err = errors.Wrap(err, "failed to initialize report DB migrations sqlite driver")
		return
	}
	m, err := migrate.NewWithInstance("iofs", sourceDriver, "sqlite", databaseDriver)
	if err != nil {
		err = errors.Wrap(err, "failed to initialize report DB schema migrator")
		return
	}
	err = m.Migrate(_schemaVersion)
	if err != nil && err != migrate.ErrNoChange {
		err = errors.Wrapf(err, "failed to migrate schemas in report DB %#v", dbPath)
		return
	}
	err = nil
	return
}
