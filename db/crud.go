package db

import (
	"context"
	"database/sql"
	"fmt"
	"time"

	"github.com/pkg/errors"
	log "github.com/sirupsen/logrus"

	"github.com/DavidArthurCole/EggLedger/api"
	"github.com/DavidArthurCole/EggLedger/ei"
)

func InsertBackup(ctx context.Context, playerId string, timestamp float64, payload []byte, minimumTimeSinceLastEntry time.Duration) error {
	action := fmt.Sprintf("insert backup for player %s into database", playerId)
	compressedPayload, err := compress(payload)
	if err != nil {
		return errors.Wrap(err, action)
	}
	return DoDBOperation(ctx, func(ctx context.Context, db *sql.DB) error {
		return transact(ctx, action, func(tx *sql.Tx) error {
			var previousTimestamp float64
			if minimumTimeSinceLastEntry.Seconds() > 0 {
				row := tx.QueryRowContext(ctx, `SELECT backed_up_at FROM backup
				WHERE player_id = ?
				ORDER BY backed_up_at DESC LIMIT 1;`, playerId)
				err := row.Scan(&previousTimestamp)
				switch {
				case err == sql.ErrNoRows:
					// No stored backup
				case err != nil:
					return err
				}
			}
			timeSinceLastEntry := time.Duration(timestamp-previousTimestamp) * time.Second
			if timeSinceLastEntry < minimumTimeSinceLastEntry {
				log.Infof("%s: %s since last recorded backup, ignoring", playerId, timeSinceLastEntry)
				return nil
			}
			_, err = tx.ExecContext(ctx, `INSERT INTO
				backup(player_id, backed_up_at, payload, payload_authenticated)
				VALUES (?, ?, ?, FALSE);`, playerId, timestamp, compressedPayload)
			if err != nil {
				return err
			}
			return nil
		})
	})
}

func InsertCompleteMission(ctx context.Context, playerId string, missionId string, startTimestamp float64, completePayload []byte) error {
	action := fmt.Sprintf("insert mission %s for player %s into database", missionId, playerId)
	compressedPayload, err := compress(completePayload)
	if err != nil {
		return errors.Wrap(err, action)
	}
	return DoDBOperation(ctx, func(ctx context.Context, db *sql.DB) error {
		return transact(ctx, action, func(tx *sql.Tx) error {
			_, err := tx.ExecContext(ctx, `INSERT INTO
				mission(player_id, mission_id, start_timestamp, complete_payload)
				VALUES (?, ?, ?, ?);`, playerId, missionId, startTimestamp, compressedPayload)
			if err != nil {
				return err
			}
			return nil
		})
	})
}

func RetrieveCompleteMission(ctx context.Context, playerId string, missionId string) (*ei.CompleteMissionResponse, error) {
	action := fmt.Sprintf("retrieve mission %s for player %s from database", missionId, playerId)
	var startTimestamp float64
	var compressedPayload []byte
	err := DoDBOperation(ctx, func(ctx context.Context, db *sql.DB) error {
		return transact(ctx, action, func(tx *sql.Tx) error {
			row := tx.QueryRowContext(ctx, `SELECT start_timestamp, complete_payload FROM mission
				WHERE player_id = ? AND mission_id = ?;`, playerId, missionId)
			err := row.Scan(&startTimestamp, &compressedPayload)
			switch {
			case err == sql.ErrNoRows:
				// No such mission
			case err != nil:
				return err
			}
			return nil
		})
	})
	if err != nil {
		return nil, err
	}
	if compressedPayload == nil {
		return nil, nil
	}
	completePayload, err := decompress(compressedPayload)
	if err != nil {
		return nil, errors.Wrap(err, action)
	}
	m, err := api.DecodeCompleteMissionPayload(completePayload)
	if err != nil {
		return nil, errors.Wrap(err, action)
	}
	m.Info.StartTimeDerived = &startTimestamp
	return m, nil
}

func RetrievePlayerCompleteMissions(ctx context.Context, playerId string) ([]*ei.CompleteMissionResponse, error) {
	action := fmt.Sprintf("retrieve complete missions for player %s from database", playerId)
	var count int
	var startTimestamps []float64
	var compressedPayloads [][]byte
	err := DoDBOperation(ctx, func(ctx context.Context, db *sql.DB) error {
		return transact(ctx, action, func(tx *sql.Tx) error {
			rows, querr := tx.QueryContext(ctx, `SELECT start_timestamp, complete_payload FROM mission
				WHERE player_id = ?
				ORDER BY start_timestamp;`, playerId)
			if querr != nil {
				return querr
			}
			defer rows.Close()
			for rows.Next() {
				var startTimestamp float64
				var compressedPayload []byte
				if scerr := rows.Scan(&startTimestamp, &compressedPayload); scerr != nil {
					return scerr
				}
				count++
				startTimestamps = append(startTimestamps, startTimestamp)
				compressedPayloads = append(compressedPayloads, compressedPayload)
			}
			if rerr := rows.Err(); rerr != nil {
				return rerr
			}
			return nil
		})
	})
	if err != nil {
		return nil, err
	}
	var missions []*ei.CompleteMissionResponse
	for i := 0; i < count; i++ {
		completePayload, cperr := decompress(compressedPayloads[i])
		if cperr != nil {
			return nil, errors.Wrap(cperr, action)
		}
		m, derr := api.DecodeCompleteMissionPayload(completePayload)
		if derr != nil {
			return nil, errors.Wrap(derr, action)
		}
		m.Info.StartTimeDerived = &startTimestamps[i]
		missions = append(missions, m)
	}
	return missions, nil
}

func RetrievePlayerCompleteMissionIds(ctx context.Context, playerId string) ([]string, error) {
	action := fmt.Sprintf("retrieve complete mission ids for player %s from database", playerId)
	var missionIds []string
	err := DoDBOperation(ctx, func(ctx context.Context, db *sql.DB) error {
		return transact(ctx, action, func(tx *sql.Tx) error {
			rows, err := tx.QueryContext(ctx, `SELECT mission_id FROM mission
				WHERE player_id = ?
				ORDER BY start_timestamp;`, playerId)
			if err != nil {
				return err
			}
			defer rows.Close()
			for rows.Next() {
				var missionId string
				if err := rows.Scan(&missionId); err != nil {
					return err
				}
				missionIds = append(missionIds, missionId)
			}
			if err := rows.Err(); err != nil {
				return err
			}
			return nil
		})
	})
	if err != nil {
		return nil, err
	}
	return missionIds, nil
}

func transact(ctx context.Context, description string, txFunc func(*sql.Tx) error) (err error) {
	tx, err := _db.BeginTx(ctx, nil)
	if err != nil {
		return errors.Wrap(err, description)
	}
	defer func() {
		if p := recover(); p != nil {
			_ = tx.Rollback()
			panic(p)
		} else if err != nil {
			_ = tx.Rollback()
			err = errors.Wrap(err, description)
		} else {
			err = tx.Commit()
			if err != nil {
				err = errors.Wrap(err, description)
			}
		}
	}()
	err = txFunc(tx)
	return err
}
