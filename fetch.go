package main

import (
	"context"
	"fmt"
	"os"
	"path/filepath"
	"regexp"
	"strings"
	"sync"
	"time"

	humanize "github.com/dustin/go-humanize"
	"github.com/pkg/errors"
	log "github.com/sirupsen/logrus"
	"golang.org/x/sync/semaphore"

	"github.com/DavidArthurCole/EggLedger/db"
)

type worker struct {
	*semaphore.Weighted
	ctx     context.Context
	cancel  context.CancelFunc
	ctxlock sync.Mutex
}

type MissionProgress struct {
	Total                   int     `json:"total"`
	Finished                int     `json:"finished"`
	Failed                  int     `json:"failed"`
	Retried                 int     `json:"retried"`
	FinishedPercentage      string  `json:"finishedPercentage"`
	ExpectedFinishTimestamp float64 `json:"expectedFinishTimestamp"`
	CurrentMission          string  `json:"currentMission"`
}

type FailedMission struct {
	missionId      string
	startTimestamp float64
}

func newWorker(n int64) *worker {
	return &worker{Weighted: semaphore.NewWeighted(n)}
}

// runFetchPipeline is the body of the fetchPlayerData binding. It acquires the
// worker semaphore, runs the full backup-fetch / mission-fetch / export pipeline,
// and updates UI state throughout. It must not be called concurrently.
func runFetchPipeline(w *worker, playerId string) {
	if !w.TryAcquire(1) {
		_emitMessage("already fetching player data, cannot accept new work", true)
		log.Error("already fetching player data, cannot accept new work")
		return
	}
	defer w.Release(1)

	ctx, cancel := context.WithCancel(context.Background())
	w.ctxlock.Lock()
	w.ctx = ctx
	w.cancel = cancel
	w.ctxlock.Unlock()

	runProc := _processRegistry.Register(
		fmt.Sprintf("fetch-%s-%d", playerId, time.Now().UnixMilli()),
		fmt.Sprintf("Fetching data for %s", playerId),
		"overall",
	)
	runProc.InitSegments([]string{"Save", "Missions", "Export"})
	origUpdateState := _updateState
	pinfo := func(args ...interface{}) {
		msg := fmt.Sprint(args...)
		log.Info(args...)
		_emitMessage(msg, false)
		runProc.Log(msg, false)
	}
	perror := func(args ...interface{}) {
		msg := fmt.Sprint(args...)
		log.Error(args...)
		_emitMessage(msg, true)
		runProc.Log(msg, true)
	}
	updateState := func(state AppState) {
		origUpdateState(state)
		switch state {
		case AppState_SUCCESS:
			runProc.MarkDone()
		case AppState_FAILED, AppState_INTERRUPTED:
			runProc.MarkFailed()
		}
	}

	checkInterrupt := func() bool {
		select {
		case <-ctx.Done():
			perror("interrupted")
			updateState(AppState_INTERRUPTED)
			return true
		default:
			return false
		}
	}

	updateState(AppState_FETCHING_SAVE)
	runProc.SetSegment("Save", "active")
	fc, err := fetchFirstContactWithContext(w.ctx, playerId)
	if err != nil {
		perror(err)
		if !checkInterrupt() {
			updateState(AppState_FAILED)
		}
		return
	}

	backup := fc.GetBackup()
	nickname := backup.GetUserName()
	eb := backup.GetEarningsBonus()
	roleColor, roleString, ebAddendum, eb, precision := RoleFromEB(eb)
	ebString := fmt.Sprintf(fmt.Sprintf("%%.%df", precision), eb) + ebAddendum

	msg := fmt.Sprintf("successfully fetched backup for &7a7a7a<%s>", playerId)
	if nickname != "" {
		msg += fmt.Sprintf(" (&%s<%s>)", roleColor, nickname)
	}
	pinfo(msg)
	runProc.SetSegment("Save", "done")

	game := backup.GetGame()
	seSuffix := AbbreviateFloat(game.GetSoulEggsD())
	peCount := int(game.GetEggsOfProphecy())
	totalTE := 0
	if virtue := backup.GetVirtue(); virtue != nil {
		for _, v := range virtue.GetEovEarned() {
			totalTE += int(v)
		}
	}
	breakdownMsg := ""
	if totalTE > 0 {
		breakdownMsg += fmt.Sprintf("  [img:truth_egg.png] &c831ff<%d TE>", totalTE)
	}
	breakdownMsg += fmt.Sprintf("  [img:soul_egg.png] &a855f7<%s SE>  [img:prophecy_egg.png] &eab308<%d PE>", seSuffix, peCount)
	pinfo(breakdownMsg)

	ebMsg := fmt.Sprintf("updated local database EB to &%s<%s>, role to &%s<%s>", roleColor, ebString, roleColor, roleString)
	pinfo(ebMsg)

	lastBackupTime := backup.GetSettings().GetLastBackupTime()
	if lastBackupTime != 0 {
		t := unixToTime(lastBackupTime)
		now := time.Now()
		if t.After(now) {
			t = now
		}
		msg := fmt.Sprintf("backup is from &7a7a7a<%s>", humanize.Time(t))
		pinfo(msg)
	} else {
		perror("backup is from unknown time")
	}
	_storage.AddKnownAccount(Account{Id: playerId, Nickname: nickname, EBString: ebString, AccountColor: roleColor, SeString: seSuffix, PeCount: peCount, TeCount: totalTE})
	_storage.Lock()
	_updateKnownAccounts(_storage.KnownAccounts)
	_storage.Unlock()
	if checkInterrupt() {
		return
	}

	missions := fc.GetCompletedMissions()
	inProgressMissions := fc.GetInProgressMissions()
	existingMissionIds, err := db.RetrievePlayerCompleteMissionIds(context.Background(), playerId)
	if err != nil {
		perror(err)
		updateState(AppState_FAILED)
		return
	}
	seen := make(map[string]struct{})
	for _, id := range existingMissionIds {
		seen[id] = struct{}{}
	}
	var newMissionIds []string
	var newMissionStartTimestamps []float64
	for _, mission := range missions {
		id := mission.GetIdentifier()
		if _, exists := seen[id]; !exists {
			newMissionIds = append(newMissionIds, id)
			newMissionStartTimestamps = append(newMissionStartTimestamps, mission.GetStartTimeDerived())
		}
	}
	pinfo(fmt.Sprintf("found &148c32<%d completed> missions, &148c32<%d in-progress> missions, &148c32<%d to fetch>",
		len(missions), len(inProgressMissions), len(newMissionIds)))

	// One-time type-resolution pass: update any cached missions whose
	// mission_type is still -1 (the DB default for missions stored before
	// the Virtue update added the type column). This decodes the locally
	// stored payload - no API calls - so it runs quickly.
	pendingCount, pcErr := db.CountPendingMissionTypes(context.Background(), playerId)
	if pcErr != nil {
		log.Error(pcErr)
		pendingCount = 0
	}
	if pendingCount > 0 {
		updateState(AppState_RESOLVING_MISSION_TYPES)
		pinfo(fmt.Sprintf("resolving mission types for &148c32<%d> cached missions", pendingCount))
		if checkInterrupt() {
			return
		}
		resolved, resErr := db.ResolvePendingMissionTypes(
			context.Background(),
			playerId,
			func(decoded, total int) {
				_updateMissionProgress(MissionProgress{
					Total:              total,
					Finished:           decoded,
					FinishedPercentage: fmt.Sprintf("%.1f%%", float64(decoded)/float64(total)*100),
				})
			},
		)
		if resErr != nil {
			perror(resErr)
			// Non-fatal: continue with the normal fetch even if resolution fails.
		} else {
			pinfo(fmt.Sprintf("resolved &148c32<%d> cached mission types", resolved))
		}
	}

	// Build label lookup for progress display: identifier -> "Ship · Duration"
	missionLabelByID := make(map[string]string)
	for _, mission := range missions {
		label := mission.GetShip().Name() + " · " + mission.GetDurationType().Display()
		missionLabelByID[mission.GetIdentifier()] = label
	}

	total := len(newMissionIds)
	finished := 0
	failed := 0
	retried := 0
	currentMission := ""
	var currentMissionMu sync.Mutex
	if total > 0 {
		updateState(AppState_FETCHING_MISSIONS)
		runProc.SetSegment("Missions", "active")

		workerCount := _storage.GetWorkerCount()
		var failureMu sync.Mutex

		reportProgress := func(finished, failedCount, retriedCount int) {
			currentMissionMu.Lock()
			label := currentMission
			currentMissionMu.Unlock()
			remainingBatches := (total - finished + workerCount - 1) / workerCount
			_updateMissionProgress(MissionProgress{
				Total:                   total,
				Finished:                finished,
				Failed:                  failedCount,
				Retried:                 retriedCount,
				FinishedPercentage:      fmt.Sprintf("%.1f%%", float64(finished)/float64(total)*100),
				ExpectedFinishTimestamp: timeToUnix(time.Now().Add(time.Duration(remainingBatches) * _requestInterval)),
				CurrentMission:          label,
			})
		}

		reportProgress(finished, failed, retried)

		finishedCh := make(chan struct{}, total)
		go func() {
			for range finishedCh {
				finished++
				failureMu.Lock()
				f, r := failed, retried
				failureMu.Unlock()
				reportProgress(finished, f, r)
			}
		}()

		errored := 0
		var wg sync.WaitGroup
		failedMissions := []FailedMission{}
		tryFailedAgain := _storage.GetRetryFailedMissions()

	MissionsLoop:
		for i := 0; i < total; i += workerCount {
			if i != 0 {
				select {
				case <-ctx.Done():
					break MissionsLoop
				case <-time.After(_requestInterval):
				}
			}
			batchEnd := min(i+workerCount, total)
			currentMissionMu.Lock()
			currentMission = fmt.Sprintf("%s · %s", missionLabelByID[newMissionIds[i]], unixToTime(newMissionStartTimestamps[i]).Format("2006-01-02"))
			currentMissionMu.Unlock()
			for j := i; j < batchEnd; j++ {
				id := newMissionIds[j]
				ts := newMissionStartTimestamps[j]
				wg.Add(1)
				go fetchOneMission(w.ctx, playerId, id, ts,
					id, fmt.Sprintf("%s · %s", missionLabelByID[id], unixToTime(ts).Format("2006-01-02")),
					_storage.GetHideTimeoutErrors(), perror,
					func() {
						failureMu.Lock()
						errored++
						failed++
						if tryFailedAgain {
							failedMissions = append(failedMissions, FailedMission{missionId: id, startTimestamp: ts})
						}
						failureMu.Unlock()
					}, &wg, finishedCh)
			}
		}
		wg.Wait()

		if checkInterrupt() {
			return
		}

		if errored > 0 {
			perror(fmt.Sprintf("%d of %d missions failed to fetch", errored, total))

			if tryFailedAgain {
				errored = 0
				retried = len(failedMissions)
				pinfo("retrying failed missions")

				// Update the total to include the number of failed missions
				total += len(failedMissions)

				for _, fm := range failedMissions {
					id := fm.missionId
					ts := fm.startTimestamp
					wg.Add(1)
					go fetchOneMission(w.ctx, playerId, id, ts,
						id+"-retry", fmt.Sprintf("%s · %s (retry)", missionLabelByID[id], unixToTime(ts).Format("2006-01-02")),
						_storage.GetHideTimeoutErrors(), perror,
						func() {
							failureMu.Lock()
							errored++
							failureMu.Unlock()
						}, &wg, finishedCh)
				}
				wg.Wait()

				if checkInterrupt() {
					return
				}

				if errored > 0 {
					perror(fmt.Sprintf("%d of %d mission retry fetches failed", errored, len(failedMissions)))
					updateState(AppState_FAILED)
					return
				} else {
					pinfo(fmt.Sprintf("successfully fetched &148c32<%d previously failed missions>", len(failedMissions)))
				}
			} else {
				pinfo("(performing another &7a7a7a<fetch> will fetch the failed missions most of the time)")
				updateState(AppState_FAILED)
				return
			}
		} else {
			pinfo(fmt.Sprintf("successfully fetched &148c32<%d missions>", total))
			runProc.SetSegment("Missions", "done")
		}
	}

	updateState(AppState_EXPORTING_DATA)
	runProc.SetSegment("Export", "active")
	completeMissions, err := db.RetrievePlayerCompleteMissions(context.Background(), playerId)
	if err != nil {
		perror(err)
		updateState(AppState_FAILED)
		return
	}
	var exportMissions []*mission
	for _, m := range completeMissions {
		exportMissions = append(exportMissions, newMission(m))
	}
	if checkInterrupt() {
		return
	}

	exportDir := filepath.Join(_rootDir, "exports", "missions")
	if err := os.MkdirAll(exportDir, 0755); err != nil {
		perror(errors.Wrap(err, "failed to create export directory"))
		updateState(AppState_FAILED)
		return
	}

	// Determine the last exported pair of xlsx and csv for future comparison.
	filenamePattern := regexp.QuoteMeta(playerId) + `\.\d{8}_\d{6}`
	lastExportedXlsxFile, err := findLastMatchingFile(exportDir, filenamePattern+`\.xlsx`)
	if err != nil {
		log.Errorf("error locating last exported .xlsx file: %s", err)
	}
	lastExportedCsvFile, err := findLastMatchingFile(exportDir, filenamePattern+`\.csv`)
	if err != nil {
		log.Errorf("error locating last exported .csv file: %s", err)
	}
	if filenameWithoutExt(lastExportedXlsxFile) != filenameWithoutExt(lastExportedCsvFile) {
		// If the xlsx and csv files aren't a pair, just leave them alone.
		lastExportedXlsxFile = ""
		lastExportedCsvFile = ""
	}

	filenameTimestamp := time.Now().Format("20060102_150405")

	xlsxFile := filepath.Join(exportDir, playerId+"."+filenameTimestamp+".xlsx")
	if err := exportMissionsToXlsx(exportMissions, xlsxFile); err != nil {
		perror(err)
		updateState(AppState_FAILED)
		return
	}
	if checkInterrupt() {
		return
	}

	csvFile := filepath.Join(exportDir, playerId+"."+filenameTimestamp+".csv")
	if err := exportMissionsToCsv(exportMissions, csvFile); err != nil {
		perror(err)
		updateState(AppState_FAILED)
		return
	}
	if checkInterrupt() {
		return
	}

	// Check if both exports are unchanged compared to the last exported pair.
	exportsUnchanged := lastExportedXlsxFile != "" && lastExportedCsvFile != "" && func() bool {
		xlsxUnchanged, err := cmpZipFiles(xlsxFile, lastExportedXlsxFile)
		if err != nil {
			log.Error(err)
			return false
		}
		if !xlsxUnchanged {
			return false
		}
		csvUnchanged, err := cmpFiles(csvFile, lastExportedCsvFile)
		if err != nil {
			log.Error(err)
			return false
		}
		if !csvUnchanged {
			return false
		}
		return true
	}()

	if exportsUnchanged {
		pinfo("exports identical with existing data files, reusing")
		err = os.Remove(xlsxFile)
		if err != nil {
			log.Errorf("error removing %s: %s", xlsxFile, err)
		}
		err = os.Remove(csvFile)
		if err != nil {
			log.Errorf("error removing %s: %s", csvFile, err)
		}

		xlsxFile = lastExportedXlsxFile
		csvFile = lastExportedCsvFile
	}
	xlsxFileRel, _ := filepath.Rel(_rootDir, xlsxFile)
	csvFileRel, _ := filepath.Rel(_rootDir, csvFile)
	_updateExportedFiles([]string{strings.TrimSpace(xlsxFileRel), strings.TrimSpace(csvFileRel)})

	runProc.SetSegment("Export", "done")
	pinfo("done.")
	updateState(AppState_SUCCESS)
}

// fetchOneMission fetches and stores a single mission, registering a per-mission
// process with Cache/Fetch/Decode/Store segment tracking. procID and procLabel
// distinguish primary runs from retries. onError is invoked on failure; the
// closure is responsible for acquiring any necessary mutex around counter mutations.
func fetchOneMission(
	ctx context.Context,
	playerId, missionId string,
	startTimestamp float64,
	procID, procLabel string,
	hideTimeouts bool,
	perror func(...interface{}),
	onError func(),
	wg *sync.WaitGroup,
	finishedCh chan<- struct{},
) {
	defer wg.Done()
	missionProc := _processRegistry.Register(procID, procLabel, "mission")
	missionProc.InitSegments([]string{"Cache", "Fetch", "Decode", "Store"})
	tracker := func(name, status string) { missionProc.SetSegment(name, status) }
	_, err := fetchCompleteMissionWithContext(ctx, playerId, missionId, startTimestamp, tracker)
	if err != nil {
		missionProc.Log(err.Error(), true)
		missionProc.MarkFailed()
		isTimeout := strings.Contains(err.Error(), "timeout after")
		if !isTimeout || !hideTimeouts {
			perror(err)
		}
		onError()
	} else {
		missionProc.Log("done.", false)
		missionProc.MarkDone()
	}
	finishedCh <- struct{}{}
}
