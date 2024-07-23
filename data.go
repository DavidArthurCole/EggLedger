package main

import (
	"context"
	"fmt"
	"path/filepath"
	"time"

	"github.com/pkg/errors"
	log "github.com/sirupsen/logrus"

	"github.com/DavidArthurCole/EggLedger/api"
	"github.com/DavidArthurCole/EggLedger/db"
	"github.com/DavidArthurCole/EggLedger/ei"
)

var _dbPath string

func dataInit() {
	_dbPath = filepath.Join(_internalDir, "data.db")
	if err := db.InitDB(_dbPath); err != nil {
		log.Fatal("dataInit() err: ", err)
	}
}

func fetchFirstContactWithContext(ctx context.Context, playerId string) (*ei.EggIncFirstContactResponse, error) {
	action := fmt.Sprintf("fetching backup for player %s", playerId)
	wrap := func(err error) error {
		return errors.Wrap(err, "error "+action)
	}
	payload, err := api.RequestFirstContactRawPayloadWithContext(ctx, playerId)
	if err != nil {
		return nil, wrap(err)
	}
	fc, err := api.DecodeFirstContactPayload(payload)
	if err != nil {
		return nil, wrap(err)
	}
	if err := fc.Validate(); err != nil {
		return nil, errors.Wrap(wrap(err), "please double check your ID")
	}
	timestamp := fc.GetBackup().GetSettings().GetLastBackupTime()
	if timestamp != 0 {
		if err := db.InsertBackup(ctx, playerId, timestamp, payload, 12*time.Hour); err != nil {
			// Treat as non-fatal error for now.
			log.Error(err)
		}
	} else {
		log.Warnf("%s: .backup.settings.last_backup_time is 0", playerId)
	}
	return fc, nil
}

func fetchCompleteMissionWithContext(ctx context.Context, playerId string, missionId string, startTimestamp float64) (*ei.CompleteMissionResponse, error) {
	action := fmt.Sprintf("fetching mission %s for player %s", missionId, playerId)
	wrap := func(err error) error {
		return errors.Wrap(err, "error "+action)
	}
	resp, err := db.RetrieveCompleteMission(ctx, playerId, missionId)
	if err != nil {
		return nil, wrap(err)
	}
	if resp != nil {
		return resp, nil
	}
	payload, err := api.RequestCompleteMissionRawPayloadWithContext(ctx, playerId, missionId)
	if err != nil {
		return nil, wrap(err)
	}
	resp, err = api.DecodeCompleteMissionPayload(payload)
	if err != nil {
		return nil, wrap(err)
	}
	if !resp.GetSuccess() {
		return nil, wrap(errors.New("success is false"))
	}
	if len(resp.GetArtifacts()) == 0 {
		return nil, wrap(errors.New("no artifact found in server response"))
	}
	err = db.InsertCompleteMission(ctx, playerId, missionId, startTimestamp, payload)
	return resp, err
}
