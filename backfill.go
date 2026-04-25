package main

import (
	"context"
	"sync/atomic"

	"github.com/DavidArthurCole/EggLedger/api"
	"github.com/DavidArthurCole/EggLedger/db"
	log "github.com/sirupsen/logrus"
)

type backfillStatus struct {
	done     atomic.Bool
	total    atomic.Int64
	finished atomic.Int64
}

var _backfill backfillStatus

// BackfillStatus is the JSON shape returned to the UI.
type BackfillStatus struct {
	Done     bool    `json:"done"`
	Progress float64 `json:"progress"`
}

func GetBackfillStatus() BackfillStatus {
	if _backfill.done.Load() {
		return BackfillStatus{Done: true, Progress: 1.0}
	}
	total := _backfill.total.Load()
	if total == 0 {
		return BackfillStatus{Done: false, Progress: 0}
	}
	return BackfillStatus{
		Done:     false,
		Progress: float64(_backfill.finished.Load()) / float64(total),
	}
}

// BackfillArtifactDrops populates artifact_drops for all missions that predate
// migration 7. Intended to run as a background goroutine; non-fatal on any
// per-mission error.
func BackfillArtifactDrops() {
	ctx := context.Background()
	refs, err := db.GetMissionsWithoutDrops(ctx)
	if err != nil {
		log.Errorf("backfill: failed to query missions without drops: %v", err)
		_backfill.done.Store(true)
		return
	}
	if len(refs) == 0 {
		_backfill.done.Store(true)
		return
	}

	_backfill.total.Store(int64(len(refs)))
	log.Infof("backfill: populating artifact_drops for %d missions", len(refs))

	for _, ref := range refs {
		payload, err := db.DecompressPayload(ref.CompressedPayload)
		if err != nil {
			log.Errorf("backfill: decompress %s: %v", ref.MissionId, err)
			_backfill.finished.Add(1)
			continue
		}
		resp, err := api.DecodeCompleteMissionPayload(payload)
		if err != nil {
			log.Errorf("backfill: decode %s: %v", ref.MissionId, err)
			_backfill.finished.Add(1)
			continue
		}
		drops := buildArtifactDropRows(resp)
		if err := db.InsertArtifactDrops(ctx, ref.PlayerId, ref.MissionId, drops); err != nil {
			log.Errorf("backfill: insert drops %s: %v", ref.MissionId, err)
		}
		_backfill.finished.Add(1)
	}
	log.Infof("backfill: artifact_drops backfill complete")
	_backfill.done.Store(true)
}
