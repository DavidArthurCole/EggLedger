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

// b2i converts a bool to its integer column representation (1 for true, 0 for
// false), matching how is_dub_cap / is_bugged_cap are stored.
func b2i(b bool) int {
	if b {
		return 1
	}
	return 0
}

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

// MissionFilterCols holds precomputed values for the indexed filter columns
// added in migration 6. These avoid full payload decompression for list queries.
type MissionFilterCols struct {
	Ship            int32
	DurationType    int32
	Level           int32
	Capacity        int32
	NominalCapacity int32
	IsDubCap        bool
	IsBuggedCap     bool
	Target          int32
	ReturnTimestamp float64
}

// ArtifactDropRow is one processed artifact drop ready for DB insertion.
// DropIndex is the 0-based position of this drop in CompleteMissionResponse.Artifacts,
// matching the UNIQUE(mission_id, player_id, drop_index) constraint in migration 7.
type ArtifactDropRow struct {
	DropIndex  int32
	ArtifactId int32
	SpecType   string
	Level      int32
	Rarity     int32
	Quality    float64
}

// MissionMeta is a lightweight mission record built purely from DB columns,
// without decompressing the payload blob.
type MissionMeta struct {
	MissionId       string
	StartTimestamp  float64
	ReturnTimestamp float64
	Ship            int32
	DurationType    int32
	Level           int32
	Capacity        int32
	NominalCapacity int32
	IsDubCap        bool
	IsBuggedCap     bool
	Target          int32
	MissionType     int32
}

func InsertCompleteMission(ctx context.Context, playerId string, missionId string, startTimestamp float64, completePayload []byte, missionType int32, cols MissionFilterCols) error {
	action := fmt.Sprintf("insert mission %s for player %s into database", missionId, playerId)
	compressedPayload, err := compress(completePayload)
	if err != nil {
		return errors.Wrap(err, action)
	}
	return DoDBOperation(ctx, func(ctx context.Context, db *sql.DB) error {
		return transact(ctx, action, func(tx *sql.Tx) error {
			_, err := tx.ExecContext(ctx, `INSERT INTO
				mission(player_id, mission_id, start_timestamp, complete_payload, mission_type,
				        ship, duration_type, level, capacity, nominal_capacity, is_dub_cap, is_bugged_cap, target, return_timestamp)
				VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);`,
				playerId, missionId, startTimestamp, compressedPayload, missionType,
				cols.Ship, cols.DurationType, cols.Level, cols.Capacity, cols.NominalCapacity,
				b2i(cols.IsDubCap), b2i(cols.IsBuggedCap), cols.Target, cols.ReturnTimestamp)
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
		// Read-only single-row lookup on the fetch hot path: query directly
		// rather than opening a write-capable transaction.
		row := db.QueryRowContext(ctx, `SELECT start_timestamp, complete_payload FROM mission
			WHERE player_id = ? AND mission_id = ?;`, playerId, missionId)
		err := row.Scan(&startTimestamp, &compressedPayload)
		if err != nil && err != sql.ErrNoRows {
			return err
		}
		return nil
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
	if m.Info != nil {
		m.Info.StartTimeDerived = &startTimestamp
	}
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
	for i := range count {
		completePayload, cperr := decompress(compressedPayloads[i])
		if cperr != nil {
			return nil, errors.Wrap(cperr, action)
		}
		m, derr := api.DecodeCompleteMissionPayload(completePayload)
		if derr != nil {
			return nil, errors.Wrap(derr, action)
		}
		if m.Info != nil {
			m.Info.StartTimeDerived = &startTimestamps[i]
		}
		missions = append(missions, m)
	}
	return missions, nil
}

// RetrievePlayerCompleteMissionsStream calls cb for each mission one at a time,
// decompressing and decoding inline so only one mission is in memory at once.
// This avoids the large transient allocation that occurs when all compressed
// payloads are loaded into a [][]byte slice before decoding.
func RetrievePlayerCompleteMissionsStream(ctx context.Context, playerId string, cb func(*ei.CompleteMissionResponse) error) error {
	action := fmt.Sprintf("stream complete missions for player %s", playerId)
	return DoDBOperation(ctx, func(ctx context.Context, db *sql.DB) error {
		return transact(ctx, action, func(tx *sql.Tx) error {
			rows, err := tx.QueryContext(ctx,
				`SELECT start_timestamp, complete_payload FROM mission WHERE player_id = ? ORDER BY start_timestamp;`,
				playerId)
			if err != nil {
				return err
			}
			defer rows.Close()
			for rows.Next() {
				var ts float64
				var compressed []byte
				if err := rows.Scan(&ts, &compressed); err != nil {
					return err
				}
				raw, err := decompress(compressed)
				if err != nil {
					return err
				}
				m, err := api.DecodeCompleteMissionPayload(raw)
				if err != nil {
					return err
				}
				if m.Info != nil {
					m.Info.StartTimeDerived = &ts
				}
				if err := cb(m); err != nil {
					return err
				}
			}
			return rows.Err()
		})
	})
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

// RetrievePlayerMissionStats returns the mission count and the maximum
// return_timestamp (Unix seconds, 0 if no missions exist) for a player.
// A single query replaces the two separate calls previously used by
// missionquery.GetExistingData.
func RetrievePlayerMissionStats(ctx context.Context, playerId string) (count int, maxReturnTS float64, err error) {
	action := fmt.Sprintf("retrieve mission stats for player %s from database", playerId)
	err = DoDBOperation(ctx, func(ctx context.Context, db *sql.DB) error {
		return transact(ctx, action, func(tx *sql.Tx) error {
			row := tx.QueryRowContext(ctx,
				`SELECT COUNT(*), COALESCE(MAX(return_timestamp), 0) FROM mission WHERE player_id = ?;`,
				playerId)
			return row.Scan(&count, &maxReturnTS)
		})
	})
	return count, maxReturnTS, err
}

// CountPendingMissionTypes returns the number of missions for a player that
// have mission_type = -1 (i.e. the type has not yet been resolved from the
// stored payload). This is used to decide whether the one-time type-resolution
// pass is needed.
func CountPendingMissionTypes(ctx context.Context, playerId string) (int, error) {
	action := fmt.Sprintf("count pending mission types for player %s", playerId)
	var count int
	err := DoDBOperation(ctx, func(ctx context.Context, db *sql.DB) error {
		return transact(ctx, action, func(tx *sql.Tx) error {
			row := tx.QueryRowContext(ctx,
				`SELECT COUNT(*) FROM mission WHERE player_id = ? AND mission_type = -1;`,
				playerId)
			return row.Scan(&count)
		})
	})
	return count, err
}

// CountPendingFilterCols returns the number of missions for a player that have
// ship = -1, meaning the migration-6 filter columns have not yet been populated.
func CountPendingFilterCols(ctx context.Context, playerId string) (int, error) {
	action := fmt.Sprintf("count pending filter columns for player %s", playerId)
	var count int
	err := DoDBOperation(ctx, func(ctx context.Context, db *sql.DB) error {
		return transact(ctx, action, func(tx *sql.Tx) error {
			row := tx.QueryRowContext(ctx,
				`SELECT COUNT(*) FROM mission WHERE player_id = ? AND ship = -1;`,
				playerId)
			return row.Scan(&count)
		})
	})
	return count, err
}

// pendingMission is one decoded mission produced by collectPendingMissions:
// the mission id, its start timestamp (0 when the caller's SELECT omits it),
// and the decoded CompleteMissionResponse.
type pendingMission struct {
	missionId      string
	startTimestamp float64
	resp           *ei.CompleteMissionResponse
}

// collectPendingMissions runs selectSQL (bound to playerId) and returns the
// decoded mission for every matching row. This performs the first two phases
// of the resolve pipeline:
//
//	phase 1: query the compressed payload rows inside one read transaction;
//	phase 2: decompress + decode each payload OUTSIDE any transaction so the
//	         CPU-heavy decode does not hold the read tx open.
//
// selectSQL must SELECT mission_id first. When hasStartTS is true it must also
// SELECT start_timestamp as the second column, followed by complete_payload;
// otherwise it must SELECT complete_payload as the second column. logTag labels
// decompress/decode warnings. progressFn, if non-nil, is called once per matched
// row with (i+1, total) - including rows skipped due to decompress/decode errors.
//
// The returned slice contains only successfully decoded missions, but progressFn
// fires for every input row, preserving the original per-iteration progress timing.
func collectPendingMissions(
	ctx context.Context,
	playerId string,
	selectSQL string,
	logTag string,
	hasStartTS bool,
	progressFn func(done, total int),
) ([]pendingMission, error) {
	type pendingRow struct {
		missionId         string
		startTimestamp    float64
		compressedPayload []byte
	}
	var pending []pendingRow

	err := DoDBOperation(ctx, func(ctx context.Context, db *sql.DB) error {
		return transact(ctx, logTag, func(tx *sql.Tx) error {
			rows, err := tx.QueryContext(ctx, selectSQL, playerId)
			if err != nil {
				return err
			}
			defer rows.Close()
			for rows.Next() {
				var r pendingRow
				if hasStartTS {
					if err := rows.Scan(&r.missionId, &r.startTimestamp, &r.compressedPayload); err != nil {
						return err
					}
				} else {
					if err := rows.Scan(&r.missionId, &r.compressedPayload); err != nil {
						return err
					}
				}
				pending = append(pending, r)
			}
			return rows.Err()
		})
	})
	if err != nil {
		return nil, err
	}

	total := len(pending)
	var decoded []pendingMission
	for i, r := range pending {
		payload, derr := decompress(r.compressedPayload)
		if derr != nil {
			log.Warnf("%s: decompress %s: %s", logTag, r.missionId, derr)
			if progressFn != nil {
				progressFn(i+1, total)
			}
			continue
		}
		resp, derr := api.DecodeCompleteMissionPayload(payload)
		if derr != nil {
			log.Warnf("%s: decode %s: %s", logTag, r.missionId, derr)
			if progressFn != nil {
				progressFn(i+1, total)
			}
			continue
		}
		decoded = append(decoded, pendingMission{
			missionId:      r.missionId,
			startTimestamp: r.startTimestamp,
			resp:           resp,
		})
		if progressFn != nil {
			progressFn(i+1, total)
		}
	}
	return decoded, nil
}

// applyMissionUpdates runs updateFn for each update inside a single write
// transaction (phase 3 of the resolve pipeline). It returns immediately with
// no DB work when updates is empty. action labels the transaction.
func applyMissionUpdates[U any](
	ctx context.Context,
	action string,
	updates []U,
	updateFn func(ctx context.Context, tx *sql.Tx, u U) error,
) error {
	if len(updates) == 0 {
		return nil
	}
	return DoDBOperation(ctx, func(ctx context.Context, db *sql.DB) error {
		return transact(ctx, action, func(tx *sql.Tx) error {
			for _, u := range updates {
				if err := updateFn(ctx, tx, u); err != nil {
					return err
				}
			}
			return nil
		})
	})
}

// ResolvePendingFilterCols decodes the stored payload for every mission with
// ship = -1 and updates all migration-6 filter columns using the provided
// computeFn. Returns the number of missions updated.
//
// computeFn receives the start timestamp and decoded CompleteMissionResponse
// and must return (MissionFilterCols, ok). If ok is false the mission is skipped.
//
// progressFn, if non-nil, is called after each decode with (done, total).
func ResolvePendingFilterCols(
	ctx context.Context,
	playerId string,
	computeFn func(startTimestamp float64, resp *ei.CompleteMissionResponse) (MissionFilterCols, bool),
	progressFn func(done, total int),
) (int, error) {
	action := fmt.Sprintf("resolve pending filter columns for player %s", playerId)

	decoded, err := collectPendingMissions(ctx, playerId,
		`SELECT mission_id, start_timestamp, complete_payload FROM mission
		 WHERE player_id = ? AND ship = -1;`,
		action, true, progressFn)
	if err != nil {
		return 0, errors.Wrap(err, action)
	}

	type resolvedRow struct {
		missionId string
		cols      MissionFilterCols
	}
	var updates []resolvedRow
	for _, m := range decoded {
		cols, ok := computeFn(m.startTimestamp, m.resp)
		if ok {
			updates = append(updates, resolvedRow{missionId: m.missionId, cols: cols})
		}
	}

	if err := applyMissionUpdates(ctx, action, updates, func(ctx context.Context, tx *sql.Tx, u resolvedRow) error {
		_, serr := tx.ExecContext(ctx,
			`UPDATE mission SET
			 ship = ?, duration_type = ?, level = ?, capacity = ?, nominal_capacity = ?,
			 is_dub_cap = ?, is_bugged_cap = ?, target = ?, return_timestamp = ?
			 WHERE player_id = ? AND mission_id = ?;`,
			u.cols.Ship, u.cols.DurationType, u.cols.Level, u.cols.Capacity, u.cols.NominalCapacity,
			b2i(u.cols.IsDubCap), b2i(u.cols.IsBuggedCap), u.cols.Target, u.cols.ReturnTimestamp,
			playerId, u.missionId,
		)
		return serr
	}); err != nil {
		return 0, errors.Wrap(err, action)
	}
	return len(updates), nil
}

// RetrievePlayerMissionMeta returns lightweight mission records for a player
// built purely from DB columns (no payload decompression). Only missions where
// ship != -1 (i.e. migration-6 columns are populated) are returned.
func RetrievePlayerMissionMeta(ctx context.Context, playerId string) ([]MissionMeta, error) {
	action := fmt.Sprintf("retrieve mission meta for player %s from database", playerId)
	var metas []MissionMeta
	err := DoDBOperation(ctx, func(ctx context.Context, db *sql.DB) error {
		return transact(ctx, action, func(tx *sql.Tx) error {
			rows, err := tx.QueryContext(ctx,
				`SELECT mission_id, start_timestamp, return_timestamp,
				        ship, duration_type, level, capacity, nominal_capacity,
				        is_dub_cap, is_bugged_cap, target, mission_type
				 FROM mission
				 WHERE player_id = ? AND ship != -1
				 ORDER BY start_timestamp;`,
				playerId)
			if err != nil {
				return err
			}
			defer rows.Close()
			for rows.Next() {
				var m MissionMeta
				var isDubCapInt, isBuggedCapInt int
				if err := rows.Scan(
					&m.MissionId, &m.StartTimestamp, &m.ReturnTimestamp,
					&m.Ship, &m.DurationType, &m.Level, &m.Capacity, &m.NominalCapacity,
					&isDubCapInt, &isBuggedCapInt, &m.Target, &m.MissionType,
				); err != nil {
					return err
				}
				m.IsDubCap = isDubCapInt != 0
				m.IsBuggedCap = isBuggedCapInt != 0
				metas = append(metas, m)
			}
			return rows.Err()
		})
	})
	return metas, err
}

// ResolvePendingMissionTypes decodes the stored payload for every mission with
// mission_type = -1 and updates the column to the type found in the payload.
// Returns the number of missions that were updated.
// This replaces the DB default (-1 sentinel) with the real type without
// hitting the network; old Standard missions decode to 0 because the proto
// field defaults to STANDARD when absent.
//
// progressFn, if non-nil, is called after each payload is decoded with the
// running decoded count and the total number of missions to process. Callers
// can use this to drive a progress indicator.
func ResolvePendingMissionTypes(ctx context.Context, playerId string, progressFn func(decoded, total int)) (int, error) {
	action := fmt.Sprintf("resolve pending mission types for player %s", playerId)

	decoded, err := collectPendingMissions(ctx, playerId,
		`SELECT mission_id, complete_payload FROM mission
		 WHERE player_id = ? AND mission_type = -1;`,
		action, false, progressFn)
	if err != nil {
		return 0, errors.Wrap(err, action)
	}

	type resolvedMission struct {
		missionId   string
		missionType int32
	}
	var updates []resolvedMission
	for _, m := range decoded {
		updates = append(updates, resolvedMission{
			missionId:   m.missionId,
			missionType: int32(m.resp.GetInfo().GetType()),
		})
	}

	if err := applyMissionUpdates(ctx, action, updates, func(ctx context.Context, tx *sql.Tx, u resolvedMission) error {
		_, serr := tx.ExecContext(ctx,
			`UPDATE mission SET mission_type = ? WHERE player_id = ? AND mission_id = ?;`,
			u.missionType, playerId, u.missionId,
		)
		return serr
	}); err != nil {
		return 0, errors.Wrap(err, action)
	}

	return len(updates), nil
}

// InsertArtifactDrops inserts artifact drop rows for one mission using INSERT OR IGNORE.
// The UNIQUE(mission_id, player_id, drop_index) constraint in migration 7 makes this
// idempotent - re-runs (e.g. from the backfill goroutine) are safe with no extra checks.
// For missions with zero drops, inserts a sentinel row (drop_index = -1) to mark the
// mission as backfill-complete.
// artifactDropInsertSQL inserts one drop row. artifactDropSentinelSQL inserts the
// zero-drop sentinel (drop_index = -1, excluded by the report engine).
const artifactDropInsertSQL = `INSERT OR IGNORE INTO artifact_drops(mission_id, player_id, drop_index, artifact_id, spec_type, level, rarity, quality)
	 VALUES (?, ?, ?, ?, ?, ?, ?, ?)`
const artifactDropSentinelSQL = `INSERT OR IGNORE INTO artifact_drops(mission_id, player_id, drop_index, artifact_id, spec_type, level, rarity, quality)
	 VALUES (?, ?, -1, 0, '', 0, 0, 0)`

func InsertArtifactDrops(ctx context.Context, playerId, missionId string, drops []ArtifactDropRow) error {
	action := fmt.Sprintf("insert artifact drops for mission %s player %s", missionId, playerId)
	return DoDBOperation(ctx, func(ctx context.Context, db *sql.DB) error {
		return transact(ctx, action, func(tx *sql.Tx) error {
			if len(drops) == 0 {
				_, err := tx.ExecContext(ctx, artifactDropSentinelSQL, missionId, playerId)
				return err
			}
			stmt, err := tx.PrepareContext(ctx, artifactDropInsertSQL)
			if err != nil {
				return err
			}
			defer stmt.Close()
			for _, d := range drops {
				if _, err := stmt.ExecContext(ctx,
					missionId, playerId, d.DropIndex, d.ArtifactId, d.SpecType, d.Level, d.Rarity, d.Quality); err != nil {
					return err
				}
			}
			return nil
		})
	})
}

// MissionDropSet groups a mission's drops for batch insertion.
type MissionDropSet struct {
	PlayerId  string
	MissionId string
	Drops     []ArtifactDropRow
}

// InsertArtifactDropsBatch inserts drops for multiple missions in a single transaction.
// Each mission with zero drops gets a sentinel row (drop_index = -1). Idempotent.
func InsertArtifactDropsBatch(ctx context.Context, sets []MissionDropSet) error {
	if len(sets) == 0 {
		return nil
	}
	action := fmt.Sprintf("batch insert artifact drops (%d missions)", len(sets))
	return DoDBOperation(ctx, func(ctx context.Context, db *sql.DB) error {
		return transact(ctx, action, func(tx *sql.Tx) error {
			// Prepare both statements once for the whole batch (potentially many
			// thousands of rows) instead of re-parsing the SQL per row.
			insertStmt, err := tx.PrepareContext(ctx, artifactDropInsertSQL)
			if err != nil {
				return err
			}
			defer insertStmt.Close()
			sentinelStmt, err := tx.PrepareContext(ctx, artifactDropSentinelSQL)
			if err != nil {
				return err
			}
			defer sentinelStmt.Close()

			for _, s := range sets {
				if len(s.Drops) == 0 {
					if _, err := sentinelStmt.ExecContext(ctx, s.MissionId, s.PlayerId); err != nil {
						return err
					}
					continue
				}
				for _, d := range s.Drops {
					if _, err := insertStmt.ExecContext(ctx,
						s.MissionId, s.PlayerId, d.DropIndex, d.ArtifactId, d.SpecType, d.Level, d.Rarity, d.Quality); err != nil {
						return err
					}
				}
			}
			return nil
		})
	})
}

// MissionPayloadRef holds the minimum data needed to backfill artifact_drops
// from a stored mission blob.
type MissionPayloadRef struct {
	MissionId         string
	PlayerId          string
	CompressedPayload []byte
}

// GetMissionsWithoutDrops returns compressed payloads for all missions that
// have no rows in artifact_drops. Used by the startup backfill goroutine.
// The CompressedPayload field in each ref holds gzip-compressed bytes; call
// db.DecompressPayload before proto-decoding.
func GetMissionsWithoutDrops(ctx context.Context) ([]MissionPayloadRef, error) {
	var refs []MissionPayloadRef
	err := DoDBOperation(ctx, func(ctx context.Context, db *sql.DB) error {
		rows, err := db.QueryContext(ctx, `
			SELECT m.mission_id, m.player_id, m.complete_payload
			FROM mission m
			WHERE NOT EXISTS (
				SELECT 1 FROM artifact_drops d
				WHERE d.mission_id = m.mission_id AND d.player_id = m.player_id
			)`)
		if err != nil {
			return err
		}
		defer rows.Close()
		for rows.Next() {
			var ref MissionPayloadRef
			if err := rows.Scan(&ref.MissionId, &ref.PlayerId, &ref.CompressedPayload); err != nil {
				return err
			}
			refs = append(refs, ref)
		}
		return rows.Err()
	})
	return refs, err
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

// GetAllSettings returns all settings as a key-value map.
func GetAllSettings(ctx context.Context) (map[string]string, error) {
	action := "get all settings"
	result := make(map[string]string)
	err := DoDBOperation(ctx, func(ctx context.Context, db *sql.DB) error {
		return transact(ctx, action, func(tx *sql.Tx) error {
			rows, err := tx.QueryContext(ctx, `SELECT key, value FROM settings;`)
			if err != nil {
				return err
			}
			defer rows.Close()
			for rows.Next() {
				var k, v string
				if err := rows.Scan(&k, &v); err != nil {
					return err
				}
				result[k] = v
			}
			return rows.Err()
		})
	})
	return result, err
}

// SetSetting upserts a single key-value setting.
func SetSetting(ctx context.Context, key, value string) error {
	action := fmt.Sprintf("set setting %q", key)
	return DoDBOperation(ctx, func(ctx context.Context, db *sql.DB) error {
		return transact(ctx, action, func(tx *sql.Tx) error {
			_, err := tx.ExecContext(ctx,
				`INSERT INTO settings(key, value) VALUES (?, ?)
				 ON CONFLICT(key) DO UPDATE SET value = excluded.value;`,
				key, value)
			return err
		})
	})
}

// SetSettings upserts multiple key-value settings in a single transaction.
func SetSettings(ctx context.Context, settings map[string]string) error {
	action := "set multiple settings"
	return DoDBOperation(ctx, func(ctx context.Context, db *sql.DB) error {
		return transact(ctx, action, func(tx *sql.Tx) error {
			for k, v := range settings {
				_, err := tx.ExecContext(ctx,
					`INSERT INTO settings(key, value) VALUES (?, ?)
					 ON CONFLICT(key) DO UPDATE SET value = excluded.value;`,
					k, v)
				if err != nil {
					return err
				}
			}
			return nil
		})
	})
}

// BackfillNominalCapacities updates missions where nominal_capacity = 0 but ship != -1
// (i.e., migration-6 columns are already resolved). The lookupFn receives the stored
// ship, duration_type, and level integers and returns the nominal capacity for that triple.
// Returns the number of rows updated.
func BackfillNominalCapacities(ctx context.Context, lookupFn func(ship, durationType, level int32) int32) (int, error) {
	action := "backfill nominal capacities"
	wrap := func(err error) error { return errors.Wrap(err, action) }

	type row struct {
		missionId    string
		playerId     string
		ship         int32
		durationType int32
		level        int32
	}
	var pending []row

	err := DoDBOperation(ctx, func(ctx context.Context, db *sql.DB) error {
		return transact(ctx, action, func(tx *sql.Tx) error {
			rows, err := tx.QueryContext(ctx,
				`SELECT mission_id, player_id, ship, duration_type, level
				 FROM mission
				 WHERE nominal_capacity = 0 AND ship != -1;`)
			if err != nil {
				return err
			}
			defer rows.Close()
			for rows.Next() {
				var r row
				if err := rows.Scan(&r.missionId, &r.playerId, &r.ship, &r.durationType, &r.level); err != nil {
					return err
				}
				pending = append(pending, r)
			}
			return rows.Err()
		})
	})
	if err != nil {
		return 0, wrap(err)
	}
	if len(pending) == 0 {
		return 0, nil
	}

	err = DoDBOperation(ctx, func(ctx context.Context, db *sql.DB) error {
		return transact(ctx, action, func(tx *sql.Tx) error {
			for _, r := range pending {
				nomCap := lookupFn(r.ship, r.durationType, r.level)
				if _, serr := tx.ExecContext(ctx,
					`UPDATE mission SET nominal_capacity = ? WHERE player_id = ? AND mission_id = ?;`,
					nomCap, r.playerId, r.missionId,
				); serr != nil {
					return serr
				}
			}
			return nil
		})
	})
	if err != nil {
		return 0, wrap(err)
	}
	return len(pending), nil
}
