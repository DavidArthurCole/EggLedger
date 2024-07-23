package db

import (
	"context"
	"database/sql"
	"os"
	"path/filepath"
	"sync"

	_ "github.com/mattn/go-sqlite3"
	"github.com/pkg/errors"
	log "github.com/sirupsen/logrus"
)

var (
	_db         *sql.DB
	_initDBOnce sync.Once
	_dbWg       sync.WaitGroup
	_dbCtx      context.Context
	_dbCancel   context.CancelFunc
)

func InitDB(path string) error {
	var err error
	_initDBOnce.Do(func() {
		log.Debugf("database path: %s", path)

		parentDir := filepath.Dir(path)
		err = os.MkdirAll(parentDir, 0o755)
		if err != nil {
			err = errors.Wrapf(err, "failed to create parent directory %#v for database", parentDir)
			return
		}

		err = runMigrations(path)
		if err != nil {
			err = errors.Wrapf(err, "error occurred during schema migrations")
			return
		}

		_db, err = sql.Open("sqlite3", path+"?_foreign_keys=on&_journal_mode=WAL&_busy_timeout=10")
		if err != nil {
			err = errors.Wrapf(err, "failed to open SQLite3 database %#v", path)
			return
		}

		_dbCtx, _dbCancel = context.WithCancel(context.Background())
		err = nil
	})
	return err
}

func CloseDB() error {
	if _db != nil {
		_dbCancel()  // cancel all ongoing operations
		_dbWg.Wait() // wait for all operations to complete
		return _db.Close()
	}
	return nil
}

// A function to perform a database operation with context and wait group
func DoDBOperation(ctx context.Context, operation func(ctx context.Context, db *sql.DB) error) error {
	_dbWg.Add(1)
	defer _dbWg.Done()

	// Create a child context that will be canceled if the parent context is canceled
	childCtx, cancel := context.WithCancel(_dbCtx)
	defer cancel()

	// Run the operation
	return operation(childCtx, _db)
}
