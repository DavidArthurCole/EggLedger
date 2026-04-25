package reportdb

import (
	"context"
	"database/sql"
	"os"
	"path/filepath"
	"sync"

	"github.com/pkg/errors"
	log "github.com/sirupsen/logrus"
	_ "modernc.org/sqlite"
)

var (
	_db       *sql.DB
	_initOnce sync.Once
	_wg       sync.WaitGroup
	_ctx      context.Context
	_cancel   context.CancelFunc
)

func InitReportDB(path string) error {
	var err error
	_initOnce.Do(func() {
		parentDir := filepath.Dir(path)
		err = os.MkdirAll(parentDir, 0o755)
		if err != nil {
			err = errors.Wrapf(err, "failed to create parent directory %#v for report DB", parentDir)
			return
		}

		err = runMigrations(path)
		if err != nil {
			err = errors.Wrapf(err, "error occurred during report DB schema migrations")
			return
		}

		_db, err = sql.Open("sqlite", path+"?_pragma=foreign_keys(1)&_pragma=journal_mode(WAL)&_pragma=busy_timeout(10000)")
		if err != nil {
			err = errors.Wrapf(err, "failed to open report DB %#v", path)
			return
		}

		log.Debugf("report DB opened: %s", path)
		_ctx, _cancel = context.WithCancel(context.Background())
		err = nil
	})
	return err
}

func CloseReportDB() error {
	if _db != nil {
		_cancel()
		_wg.Wait()
		return _db.Close()
	}
	return nil
}

func DoReportDBOperation(ctx context.Context, operation func(ctx context.Context, db *sql.DB) error) error {
	_wg.Add(1)
	defer _wg.Done()
	childCtx, cancel := context.WithCancel(_ctx)
	defer cancel()
	return operation(childCtx, _db)
}
