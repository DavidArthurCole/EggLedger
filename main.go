package main

import (
	"bytes"
	"context"
	"database/sql"
	"embed"
	"encoding/json"
	"flag"
	"fmt"
	"image"
	"image/png"
	"io/fs"
	"net"
	"net/http"
	"os"
	"os/exec"
	"os/signal"
	"path/filepath"
	"regexp"
	"runtime"
	"strings"
	"sync"
	"time"

	"github.com/pkg/errors"

	"github.com/DavidArthurCole/EggLedger/api"
	"github.com/DavidArthurCole/EggLedger/db"
	"github.com/DavidArthurCole/EggLedger/ei"
	"github.com/DavidArthurCole/EggLedger/eiafx"
	"github.com/DavidArthurCole/EggLedger/ledgerdata"
	"github.com/DavidArthurCole/EggLedger/platform"
	"github.com/DavidArthurCole/EggLedger/reportdb"
	"github.com/DavidArthurCole/EggLedger/reports"
	"github.com/davidarthurcole/lorca"
	log "github.com/sirupsen/logrus"
	"github.com/sirupsen/logrus/hooks/writer"
	"github.com/google/uuid"
	"github.com/skratchdot/open-golang/open"
	"gopkg.in/natefinch/lumberjack.v2"
)

var (
	//go:embed VERSION
	_appVersion string

	//go:embed www/dist
	_fs embed.FS

	_rootDir     string
	_internalDir string

	_appIsInForbiddenDirectory bool

	// macOS Gateway security feature which executed apps with xattr
	// com.apple.quarantine in certain locations (like ~/Downloads) in a jailed
	// readonly environment. The jail looks like:
	// /private/var/folders/<...>/<...>/T/AppTranslocation/<UUID>/d/internal
	_appIsTranslocated bool

	_devMode               = os.Getenv("DEV_MODE") != ""
	_eiAfxConfigMissions   []*ei.ArtifactsConfigurationResponse_MissionParameters
	_eiAfxConfigArtis      []*ei.ArtifactsConfigurationResponse_ArtifactParameters
	_nominalShipCapacities = map[ei.MissionInfo_Spaceship]map[ei.MissionInfo_DurationType][]float32{}
	_latestMennoData       = MennoData{}
	_possibleTargets       = []PossibleTarget{}
	_possibleArtifacts     = []PossibleArtifact{}

	_forceMennoRefresh bool
	_forceUpdateCheck  bool
	_debugCompression  bool
	_launchedBrowser   string

	_processRegistry             *ProcessRegistry
	_updateKnownAccounts         func([]Account)
	_updateState                 func(AppState)
	_updateMissionProgress       func(MissionProgress)
	_updateMennoDownloadProgress func(MennoDownloadProgress)
	_updateExportedFiles         func([]string)
	_emitMessage                 func(string, bool)

	_nominalShipCapacitiesOnce sync.Once
	_possibleTargetsOnce       sync.Once
	_possibleArtifactsOnce     sync.Once
)

const (
	_requestInterval = 3 * time.Second
)

type UI struct {
	lorca.UI
}

func (u UI) MustLoad(url string) {
	err := u.Load(url)
	if err != nil {
		log.Fatal("MustLoad err: ", err)
	}
}

func (u UI) MustBind(name string, f interface{}) {
	err := u.Bind(name, f)
	if err != nil {
		log.Fatal("MustBind err: ", err)
	}
}

func (u UI) pushJSON(fn string, data any) {
	encoded, err := json.Marshal(data)
	if err != nil {
		log.Error(err)
		return
	}
	u.Eval(fmt.Sprintf("window.%s(%s)", fn, encoded))
}

type AppState string

const (
	AppState_AWAITING_INPUT          AppState = "AwaitingInput"
	AppState_FETCHING_SAVE           AppState = "FetchingSave"
	AppState_RESOLVING_MISSION_TYPES AppState = "ResolvingMissionTypes"
	AppState_FETCHING_MISSIONS       AppState = "FetchingMissions"
	AppState_EXPORTING_DATA          AppState = "ExportingData"
	AppState_SUCCESS                 AppState = "Success"
	AppState_FAILED                  AppState = "Failed"
	AppState_INTERRUPTED             AppState = "Interrupted"
)

type ExportedFile struct {
	File     string `json:"file"`
	Count    int    `json:"count"`
	DateTime string `json:"datetime"`
	EID      string `json:"eid"`
}

type DatabaseAccount struct {
	Id           string `json:"id"`
	Nickname     string `json:"nickname"`
	MissionCount int    `json:"missionCount"`
	EBString     string `json:"ebString"`
	AccountColor string `json:"accountColor"`
}

type PossibleTarget struct {
	DisplayName string `json:"displayName"`
	Id          int32  `json:"id"`
	ImageString string `json:"imageString"`
}

type PossibleArtifact struct {
	Name        ei.ArtifactSpec_Name `json:"name"`
	ProtoName   string               `json:"protoName"`
	DisplayName string               `json:"displayName"`
	Level       int32                `json:"level"`
	Rarity      int32                `json:"rarity"`
	BaseQuality float64              `json:"baseQuality"`
}

type FilterValueOption struct {
	Text       string `json:"text"`
	Value      string `json:"value"`
	StyleClass string `json:"styleClass"`
	ImagePath  string `json:"imagePath"`
	Rarity     int32  `json:"rarity"`
}

type ReleaseInfo struct {
	Body string `json:"body"`
}

func init() {
	log.SetLevel(log.InfoLevel)
	// Send a copy of logs to $TMPDIR/EggLedger.log in case the app crashes
	// before we can even set up persistent logging.
	tmplog, err := os.OpenFile(filepath.Join(os.TempDir(), "EggLedger.log"),
		os.O_CREATE|os.O_WRONLY|os.O_APPEND, 0666)
	if err != nil {
		log.Error(err)
	}
	log.AddHook(&writer.Hook{
		Writer:    tmplog,
		LogLevels: log.AllLevels,
	})

	path, err := os.Executable()
	if err != nil {
		log.Fatal("os.Executable() err: ", err)
	}
	path, err = filepath.EvalSymlinks(path)
	if err != nil {
		log.Fatal("filepath.EvalSymlinks() err: ", err)
	}
	_rootDir = filepath.Dir(path)
	if runtime.GOOS == "darwin" {
		// Locate parent dir of app bundle if we're inside a Mac app.
		parent, dir1 := filepath.Split(_rootDir)
		parent = filepath.Clean(parent)
		parent, dir2 := filepath.Split(parent)
		parent = filepath.Clean(parent)
		if dir1 == "MacOS" && dir2 == "Contents" && strings.HasSuffix(parent, ".app") {
			_rootDir = filepath.Dir(parent)
		}
	}
	log.Infof("root dir: %s", _rootDir)

	_internalDir = resolveInternalDir(_rootDir)

	// Make sure the app isn't located in the system/user app directory or
	// downloads dir.
	if runtime.GOOS == "darwin" {
		if _rootDir == "/Applications" {
			_appIsInForbiddenDirectory = true
		} else {
			pattern := regexp.MustCompile(`^/Users/[^/]+/(Applications|Downloads)$`)
			if pattern.MatchString(_rootDir) {
				_appIsInForbiddenDirectory = true
			}
		}
	} else {
		// On non-macOS platforms, just check whether the root dir ends in "/Downloads".
		pattern := regexp.MustCompile(`[\/]Downloads$`)
		if pattern.MatchString(_rootDir) {
			_appIsInForbiddenDirectory = true
		}
	}
	if _appIsInForbiddenDirectory {
		log.Error("app is in a forbidden directory")
		return
	}

	if runtime.GOOS == "darwin" {
		if strings.HasPrefix(_rootDir, "/private/var/folders/") {
			_appIsTranslocated = true
		}
	}
	if _appIsTranslocated {
		log.Error("app is translocated")
		return
	}

	if err := os.MkdirAll(_internalDir, 0755); err != nil {
		log.Fatal("MkdirAll err: ", err)
	}
	if err := platform.Hide(_internalDir); err != nil {
		log.Errorf("error hiding internal directory: %s", err)
	}

	// Set up persistent logging.
	logdir := filepath.Join(_rootDir, "logs")
	if err := os.MkdirAll(logdir, 0755); err != nil {
		log.Error(err)
	} else {
		logfile := filepath.Join(logdir, "app.log")
		logger := &lumberjack.Logger{
			Filename:  logfile,
			MaxSize:   5, // megabytes
			MaxAge:    7, // days
			LocalTime: true,
			Compress:  true,
		}
		log.AddHook(&writer.Hook{
			Writer:    logger,
			LogLevels: log.AllLevels,
		})
	}

	if err := eiafx.LoadConfig(_internalDir); err != nil {
		log.Warnf("eiafx config load failed, using embedded fallback: %v", err)
	}
	_eiAfxConfigMissions = eiafx.Config.MissionParameters
	_eiAfxConfigArtis = eiafx.Config.ArtifactParameters

	if err := ledgerdata.LoadConfig(_internalDir); err != nil {
		log.Fatal("ledgerdata.LoadConfig: ", err)
	}

	dataInit()
	storageInit()
}

func initNominalShipCapacities() {
	//Loop through ships, for each duration, get the capacity - generate the capacities for each level with capacity + (cap increase * level)
	for _, mission := range eiafx.Config.MissionParameters {
		durations := mission.GetDurations()
		_nominalShipCapacities[mission.GetShip()] = map[ei.MissionInfo_DurationType][]float32{}
		for _, duration := range durations {
			_nominalShipCapacities[mission.GetShip()][duration.GetDurationType()] = []float32{}
			if len(mission.GetLevelMissionRequirements()) == 0 {
				_nominalShipCapacities[mission.GetShip()][duration.GetDurationType()] = append(_nominalShipCapacities[mission.GetShip()][duration.GetDurationType()], float32(duration.GetCapacity()))
			} else {
				for level := 0; level <= len(mission.GetLevelMissionRequirements()); level++ {
					_nominalShipCapacities[mission.GetShip()][duration.GetDurationType()] = append(_nominalShipCapacities[mission.GetShip()][duration.GetDurationType()], float32(duration.GetCapacity())+(float32(duration.GetLevelCapacityBump())*float32(level)))
				}
			}
		}
	}
}

func viewMissionsOfId(ctx context.Context, eid string) ([]DatabaseMission, error) {
	_nominalShipCapacitiesOnce.Do(initNominalShipCapacities)

	// Fast path: if all missions have been backfilled with migration-6 filter
	// columns, build the list from DB columns only (no payload decompression).
	pending, countErr := db.CountPendingFilterCols(ctx, eid)
	if countErr == nil && pending == 0 {
		metas, err := db.RetrievePlayerMissionMeta(ctx, eid)
		if err != nil {
			log.Error(err)
			return nil, err
		}
		missions := make([]DatabaseMission, 0, len(metas))
		for _, meta := range metas {
			missions = append(missions, missionMetaToDBMission(meta))
		}
		return missions, nil
	}

	// Slow path: decompress and decode every payload (used before backfill runs).
	completeMissions, err := db.RetrievePlayerCompleteMissions(ctx, eid)
	if err != nil {
		log.Error(err)
		return nil, err
	}
	missionArr := []DatabaseMission{}
	for _, completeMission := range completeMissions {
		missionArr = append(missionArr, compileMissionInformation(completeMission))
	}

	// Kick off a background backfill so the next View call uses the fast path.
	// This is the one-time migration for users who view without fetching first.
	if countErr == nil && pending > 0 {
		go func() {
			if _, err := db.ResolvePendingFilterCols(
				context.Background(), eid, computeMissionFilterCols, nil,
			); err != nil {
				log.Errorf("background filter col backfill for %s: %s", eid, err)
			}
		}()
	}

	return missionArr, nil
}

func properTargetName(name *ei.ArtifactSpec_Name) string {
	if name == nil {
		return ""
	} else {
		return ei.ArtifactSpec_Name_name[int32(*name)]
	}
}

func initPossibleTargets() {
	possibleTargets := []PossibleTarget{
		{DisplayName: "None (Pre 1.27)", Id: -1, ImageString: "none.png"},
	}
	for _, target := range ledgerdata.Config.ArtifactTargets {
		possibleTargets = append(possibleTargets, PossibleTarget{
			DisplayName: target.DisplayName,
			Id:          int32(ei.ArtifactSpec_Name_value[target.Name]),
			ImageString: target.ImageString,
		})
	}
	_possibleTargets = possibleTargets
}

func getMaxQuality() float32 {
	maxQuality := float32(0)
	for _, mission := range _eiAfxConfigMissions {
		for _, duration := range mission.GetDurations() {
			compedMaxQuality := float32(duration.GetMaxQuality()) + (duration.GetLevelQualityBump() * float32(len(mission.LevelMissionRequirements)))
			if compedMaxQuality > maxQuality {
				maxQuality = compedMaxQuality
			}
		}
	}
	return maxQuality
}

func initPossibleArtifacts() {
	possibleArtifacts := []PossibleArtifact{}
	maxQuality := getMaxQuality()

	for _, artifact := range _eiAfxConfigArtis {
		if float64(maxQuality) >= artifact.GetBaseQuality() {
			possibleArtifact := PossibleArtifact{
				Name:        *artifact.Spec.Name,
				ProtoName:   artifact.Spec.Name.String(),
				DisplayName: artifact.Spec.CasedSmallName(),
				Level:       int32(artifact.Spec.GetLevel()),
				Rarity:      int32(artifact.Spec.GetRarity()),
				BaseQuality: float64(artifact.GetBaseQuality()),
			}
			possibleArtifacts = append(possibleArtifacts, possibleArtifact)
		}
	}

	_possibleArtifacts = possibleArtifacts
}

func checkApiVersionStaleness() bool {
	last := _storage.GetLastKnownGoodApiVersion()
	if last == "" {
		return false
	}
	return last != api.AppVersion
}

func reportDefToRow(def reports.ReportDefinition) (reportdb.ReportRow, error) {
	wrap := func(err error) error { return errors.Wrap(err, "reportDefToRow") }
	filtersBytes, err := json.Marshal(def.Filters)
	if err != nil {
		return reportdb.ReportRow{}, wrap(err)
	}
	r := reportdb.ReportRow{
		Id: def.Id, AccountId: def.AccountId, Name: def.Name,
		Subject: def.Subject, Mode: def.Mode, DisplayMode: def.DisplayMode,
		GroupBy: def.GroupBy, FiltersJSON: string(filtersBytes),
		GridX: def.GridX, GridY: def.GridY, GridW: def.GridW, GridH: def.GridH,
		Weight: def.Weight, Color: def.Color,
		Description: def.Description, ChartType: def.ChartType,
		SortOrder: def.SortOrder,
		ValueFilterOp: def.ValueFilterOp,
		ValueFilterThreshold: def.ValueFilterThreshold,
		GroupId: def.GroupId,
	}
	if def.TimeBucket != "" {
		r.TimeBucket = sql.NullString{String: def.TimeBucket, Valid: true}
	}
	if def.CustomBucketN > 0 {
		r.CustomBucketN = sql.NullInt64{Int64: int64(def.CustomBucketN), Valid: true}
	}
	if def.CustomBucketUnit != "" {
		r.CustomBucketUnit = sql.NullString{String: def.CustomBucketUnit, Valid: true}
	}
	if def.NormalizeBy != "" {
		r.NormalizeBy = sql.NullString{String: def.NormalizeBy, Valid: true}
	}
	return r, nil
}

func rowToReportDef(r reportdb.ReportRow) (reports.ReportDefinition, error) {
	wrap := func(err error) error { return errors.Wrap(err, "rowToReportDef") }
	var filters reports.ReportFilters
	if err := json.Unmarshal([]byte(r.FiltersJSON), &filters); err != nil {
		return reports.ReportDefinition{}, wrap(err)
	}
	chartType := r.ChartType
	if chartType == "" {
		chartType = "bar"
	}
	def := reports.ReportDefinition{
		Id: r.Id, AccountId: r.AccountId, Name: r.Name,
		Subject: r.Subject, Mode: r.Mode, DisplayMode: r.DisplayMode,
		GroupBy: r.GroupBy, Filters: filters,
		GridX: r.GridX, GridY: r.GridY, GridW: r.GridW, GridH: r.GridH,
		Weight: r.Weight, Color: r.Color,
		Description: r.Description, ChartType: chartType,
		SortOrder: r.SortOrder,
		CreatedAt: r.CreatedAt, UpdatedAt: r.UpdatedAt,
		ValueFilterOp: r.ValueFilterOp,
		ValueFilterThreshold: r.ValueFilterThreshold,
		GroupId: r.GroupId,
	}
	if r.TimeBucket.Valid {
		def.TimeBucket = r.TimeBucket.String
	}
	if r.CustomBucketN.Valid {
		def.CustomBucketN = int(r.CustomBucketN.Int64)
	}
	if r.CustomBucketUnit.Valid {
		def.CustomBucketUnit = r.CustomBucketUnit.String
	}
	if r.NormalizeBy.Valid {
		def.NormalizeBy = r.NormalizeBy.String
	}
	return def, nil
}

func main() {
	flag.BoolVar(&_forceMennoRefresh, "force-menno-refresh", false, "treat menno data as stale regardless of last refresh time")
	flag.BoolVar(&_forceUpdateCheck, "force-update-check", false, "bypass the 12-hour update-check cooldown")
	flag.BoolVar(&_debugCompression, "debug-compression", false, "log Content-Encoding headers on API responses to check if the server is gzip-compressing")
	flag.Parse()
	if _replacePID != 0 && _replacePath != "" {
		runReplaceMode(_replacePID, _replacePath)
		return
	}
	api.DebugCompression = _debugCompression

	if _devMode {
		log.Info("starting app in dev mode")
	}

	prefBrowserPath := _storage.PreferredChromiumPath
	browser := lorca.LocateChrome(prefBrowserPath)
	_launchedBrowser = browser
	if prefBrowserPath != browser {
		_storage.SetPreferredChromiumPath("")
	}
	if browser == "" {
		lorca.PromptDownload()
		log.Fatal("unable to locate a supported browser")
		return
	}

	isFirefox := strings.Contains(strings.ToLower(browser), "firefox")

	args := []string{}
	if !isFirefox {
		args = append(args, "--disable-features=TranslateUI,BlinkGenPropertyTrees")
	}
	/* User preference args */
	scalingFactor := _storage.GetDefaultScalingFactor()
	if !isFirefox && scalingFactor != 1.0 {
		args = append(args, "--force-device-scale-factor="+fmt.Sprintf("%f", scalingFactor))
	}
	if !isFirefox && _storage.StartInFullscreen {
		args = append(args, "--start-fullscreen")
	}
	if !isFirefox && runtime.GOOS == "linux" {
		args = append(args, "--class=Lorca")
	}
	widthPreference, heightPreference := func() (int, int) {
		resolutionPrefs := _storage.GetDefaultResolution()
		return resolutionPrefs[0], resolutionPrefs[1]
	}()
	appIconPath := buildAppIconPath()
	u, err := lorca.New("", "", browser, widthPreference, heightPreference, appIconPath, args...)
	if err != nil {
		log.Fatal("lorca err: ", err)
	}
	ui := UI{u}
	defer ui.Close()
	_ui = ui
	ui.MustBind("downloadAndInstallUpdate", handleDownloadAndInstallUpdate)

	_updateKnownAccounts = func(accounts []Account) {
		ui.pushJSON("updateKnownAccounts", accounts)
	}
	_updateState = func(state AppState) {
		ui.Eval(fmt.Sprintf("window.updateState('%s')", state))
	}
	_updateMissionProgress = func(progress MissionProgress) {
		ui.pushJSON("updateMissionProgress", progress)
	}
	_updateMennoDownloadProgress = func(progress MennoDownloadProgress) {
		ui.pushJSON("updateMennoDownloadProgress", progress)
	}
	_updateExportedFiles = func(files []string) {
		ui.pushJSON("updateExportedFiles", files)
	}
	_emitMessage = func(message string, isError bool) {
		encoded, err := json.Marshal(message)
		if err != nil {
			log.Error(err)
			return
		}
		ui.Eval(fmt.Sprintf("window.emitMessage(%s, %t)", encoded, isError))
	}

	updateProcesses := func(snapshots []ProcessSnapshot) {
		ui.pushJSON("updateProcesses", snapshots)
	}
	_processRegistry = NewProcessRegistry(updateProcesses)

	_onDiscordAuthComplete = func(connected bool, username string) {
		encoded, err := json.Marshal(username)
		if err != nil {
			log.Error(err)
			return
		}
		ui.Eval(fmt.Sprintf("window.onDiscordAuthComplete(%t, %s)", connected, encoded))
	}
	_onCloudSyncComplete = func(success bool, errMsg string) {
		encoded, err := json.Marshal(errMsg)
		if err != nil {
			log.Error(err)
			return
		}
		ui.Eval(fmt.Sprintf("window.onCloudSyncComplete(%t, %s)", success, encoded))
	}
	_onCloudRestoreComplete = func(success bool, errMsg string) {
		encoded, err := json.Marshal(errMsg)
		if err != nil {
			log.Error(err)
			return
		}
		ui.Eval(fmt.Sprintf("window.onCloudRestoreComplete(%t, %s)", success, encoded))
	}
	_processRegistry.Start(context.Background())

	reportDBPath := filepath.Join(_internalDir, "reports.db")
	if err := reportdb.InitReportDB(reportDBPath); err != nil {
		log.Fatal("reportdb init err: ", err)
	}
	defer reportdb.CloseReportDB()
	reports.SetMissionDB(db.GetDB())
	go BackfillArtifactDrops()

	ui.MustBind("getDefaultScalingFactor", func() float64 {
		return _storage.GetDefaultScalingFactor()
	})

	ui.MustBind("setDefaultScalingFactor", func(factor float64) {
		_storage.SetDefaultScalingFactor(factor)
	})

	ui.MustBind("getDefaultResolution", func() []int {
		return _storage.GetDefaultResolution()
	})

	ui.MustBind("setDefaultResolution", func(x, y int) {
		_storage.SetDefaultResolution(x, y)
	})

	ui.MustBind("setPreferredBrowser", func(path string) bool {
		if path == "" {
			return false
		}
		if _storage.PreferredChromiumPath == path {
			return false
		}
		_storage.SetPreferredChromiumPath(path)
		return true
	})

	ui.MustBind("getDetectedBrowsers", func() []string {
		lorca.RefreshFoundPaths()
		return lorca.FoundPaths()
	})

	ui.MustBind("getPreferredBrowser", func() string {
		return _storage.PreferredChromiumPath
	})

	ui.MustBind("getLoadedBrowser", func() string {
		return _launchedBrowser
	})

	ui.MustBind("restartApp", func() {
		executable, err := os.Executable()
		if err != nil {
			log.Errorf("restartApp: %v", err)
			return
		}
		cmd := exec.Command(executable, os.Args[1:]...)
		if err := cmd.Start(); err != nil {
			log.Errorf("restartApp: failed to start new instance: %v", err)
			return
		}
		ui.Close()
	})

	ui.MustBind("setAutoRefreshMennoPreference", func(flag bool) {
		_storage.SetAutoRefreshMennoPref(flag)
	})

	ui.MustBind("getAutoRefreshMennoPreference", func() bool {
		return _storage.AutoRefreshMennoPref
	})

	ui.MustBind("getStartInFullscreen", func() bool {
		_storage.Lock()
		defer _storage.Unlock()
		return _storage.StartInFullscreen
	})

	ui.MustBind("setStartInFullscreen", func(flag bool) {
		_storage.SetStartInFullscreen(flag)
	})

	ui.MustBind("appVersion", func() string {
		return _appVersion
	})

	ui.MustBind("appDirectory", func() string {
		return _rootDir
	})

	ui.MustBind("appIsInForbiddenDirectory", func() bool {
		return _appIsInForbiddenDirectory
	})

	ui.MustBind("appIsTranslocated", func() bool {
		return _appIsTranslocated
	})

	ui.MustBind("knownAccounts", func() []Account {
		_storage.Lock()
		defer _storage.Unlock()
		return _storage.KnownAccounts
	})

	ui.MustBind("filterWarningRead", func() bool {
		_storage.Lock()
		defer _storage.Unlock()
		return _storage.FilterWarningRead
	})

	ui.MustBind("setFilterWarningRead", func(flag bool) {
		_storage.SetFilterWarningRead(flag)
	})

	ui.MustBind("workerCountWarningRead", func() bool {
		_storage.Lock()
		defer _storage.Unlock()
		return _storage.WorkerCountWarningRead
	})

	ui.MustBind("setWorkerCountWarningRead", func(flag bool) {
		_storage.SetWorkerCountWarningRead(flag)
	})

	ui.MustBind("getMaxQuality", getMaxQuality)

	ui.MustBind("getAutoRetryPreference", func() bool {
		return _storage.GetRetryFailedMissions()
	})

	ui.MustBind("setAutoRetryPreference", func(flag bool) {
		_storage.SetRetryFailedMissions(flag)
	})

	ui.MustBind("getHideTimeoutErrors", func() bool {
		return _storage.GetHideTimeoutErrors()
	})

	ui.MustBind("setHideTimeoutErrors", func(flag bool) {
		_storage.SetHideTimeoutErrors(flag)
	})

	ui.MustBind("getScreenshotSafety", func() bool {
		return _storage.GetScreenshotSafety()
	})

	ui.MustBind("setScreenshotSafety", func(flag bool) {
		_storage.SetScreenshotSafety(flag)
	})

	ui.MustBind("getShowMissionProgress", func() bool {
		return _storage.GetShowMissionProgress()
	})

	ui.MustBind("setShowMissionProgress", func(flag bool) {
		_storage.SetShowMissionProgress(flag)
	})

	ui.MustBind("getCollapseOlderSections", func() bool {
		return _storage.GetCollapseOlderSections()
	})

	ui.MustBind("setCollapseOlderSections", func(flag bool) {
		_storage.SetCollapseOlderSections(flag)
	})

	ui.MustBind("getAdvancedDropFilter", func() bool {
		return _storage.GetAdvancedDropFilter()
	})

	ui.MustBind("setAdvancedDropFilter", func(flag bool) {
		_storage.SetAdvancedDropFilter(flag)
	})

	ui.MustBind("getMissionViewByDate", func() bool {
		return _storage.GetMissionViewByDate()
	})
	ui.MustBind("setMissionViewByDate", func(flag bool) {
		_storage.SetMissionViewByDate(flag)
	})

	ui.MustBind("getMissionViewTimes", func() bool {
		return _storage.GetMissionViewTimes()
	})
	ui.MustBind("setMissionViewTimes", func(flag bool) {
		_storage.SetMissionViewTimes(flag)
	})

	ui.MustBind("getMissionRecolorDC", func() bool {
		return _storage.GetMissionRecolorDC()
	})
	ui.MustBind("setMissionRecolorDC", func(flag bool) {
		_storage.SetMissionRecolorDC(flag)
	})

	ui.MustBind("getMissionRecolorBC", func() bool {
		return _storage.GetMissionRecolorBC()
	})
	ui.MustBind("setMissionRecolorBC", func(flag bool) {
		_storage.SetMissionRecolorBC(flag)
	})

	ui.MustBind("getMissionShowExpectedDrops", func() bool {
		return _storage.GetMissionShowExpectedDrops()
	})
	ui.MustBind("setMissionShowExpectedDrops", func(flag bool) {
		_storage.SetMissionShowExpectedDrops(flag)
	})

	ui.MustBind("getMissionMultiViewMode", func() string {
		return _storage.GetMissionMultiViewMode()
	})
	ui.MustBind("setMissionMultiViewMode", func(mode string) {
		_storage.SetMissionMultiViewMode(mode)
	})

	ui.MustBind("getMissionSortMethod", func() string {
		return _storage.GetMissionSortMethod()
	})

	ui.MustBind("setMissionSortMethod", func(mode string) {
		_storage.SetMissionSortMethod(mode)
	})

	ui.MustBind("getLifetimeSortMethod", func() string {
		return _storage.GetLifetimeSortMethod()
	})
	ui.MustBind("setLifetimeSortMethod", func(mode string) {
		_storage.SetLifetimeSortMethod(mode)
	})

	ui.MustBind("getLifetimeShowDropsPerShip", func() bool {
		return _storage.GetLifetimeShowDropsPerShip()
	})
	ui.MustBind("setLifetimeShowDropsPerShip", func(flag bool) {
		_storage.SetLifetimeShowDropsPerShip(flag)
	})

	ui.MustBind("getLifetimeShowExpectedTotals", func() bool {
		return _storage.GetLifetimeShowExpectedTotals()
	})
	ui.MustBind("setLifetimeShowExpectedTotals", func(flag bool) {
		_storage.SetLifetimeShowExpectedTotals(flag)
	})

	ui.MustBind("getWorkerCount", func() int {
		return _storage.GetWorkerCount()
	})

	ui.MustBind("setWorkerCount", func(n int) {
		_storage.SetWorkerCount(n)
	})

	ui.MustBind("getAfxConfigs", func() []PossibleArtifact {
		_possibleArtifactsOnce.Do(initPossibleArtifacts)
		return _possibleArtifacts
	})

	ui.MustBind("getPossibleTargets", func() []PossibleTarget {
		_possibleTargetsOnce.Do(initPossibleTargets)
		return _possibleTargets
	})

	w := newWorker(1)
	ui.MustBind("fetchPlayerData", func(playerId string) {
		go runFetchPipeline(w, playerId)
	})

	ui.MustBind("stopFetchingPlayerData", func() {
		w.ctxlock.Lock()
		defer w.ctxlock.Unlock()
		if w.cancel != nil {
			w.cancel()
		}
	})

	ui.MustBind("getExistingData", handleGetExistingData)

	ui.MustBind("getMissionIds", handleGetMissionIds)

	ui.MustBind("viewMissionsOfEid", handleViewMissionsOfEid)

	ui.MustBind("getDurationConfigs", handleGetDurationConfigs)

	ui.MustBind("getShipDrops", handleGetShipDrops)

	ui.MustBind("getAllPlayerDrops", handleGetAllPlayerDrops)

	ui.MustBind("getMissionInfo", handleGetMissionInfo)

	ui.MustBind("openFile", func(file string) {
		path := filepath.Join(_rootDir, file)
		if err := open.Start(path); err != nil {
			log.Errorf("opening %s: %s", path, err)
		}
	})

	ui.MustBind("openFileInFolder", func(file string) {
		path := filepath.Join(_rootDir, file)
		if err := platform.OpenFolderAndSelect(path); err != nil {
			log.Errorf("opening %s in folder: %s", path, err)
		}
	})

	ui.MustBind("chooseFolderPath", func() string {
		return platform.ChooseFolder()
	})

	ui.MustBind("getStoragePath", func() string {
		return _internalDir
	})

	ui.MustBind("setStorageFolderVisible", func(visible bool) {
		if visible {
			if err := platform.Show(_internalDir); err != nil {
				log.Errorf("setStorageFolderVisible show: %s", err)
			}
		} else {
			if err := platform.Hide(_internalDir); err != nil {
				log.Errorf("setStorageFolderVisible hide: %s", err)
			}
		}
	})

	ui.MustBind("backupStorageTo", func(destPath string) error {
		action := "backupStorageTo"
		wrap := func(err error) error { return errors.Wrap(err, action) }
		if destPath == "" {
			return wrap(errors.New("destination path is required"))
		}
		if err := copyDir(_internalDir, destPath); err != nil {
			return wrap(err)
		}
		return nil
	})

	ui.MustBind("moveStorageTo", func(destPath string) error {
		action := "moveStorageTo"
		wrap := func(err error) error { return errors.Wrap(err, action) }

		absInternal, err := filepath.Abs(_internalDir)
		if err != nil {
			return wrap(err)
		}
		absDest, err := filepath.Abs(destPath)
		if err != nil {
			return wrap(errors.New("invalid destination path"))
		}

		if absDest == "" {
			return wrap(errors.New("destination path is required"))
		}
		if absDest == absInternal {
			return wrap(errors.New("destination is the same as current location"))
		}
		if strings.HasPrefix(absDest, absInternal+string(filepath.Separator)) {
			return wrap(errors.New("destination cannot be inside the current storage directory"))
		}

		destPreExisted := false
		if _, statErr := os.Stat(absDest); statErr == nil {
			destPreExisted = true
		}

		probe := filepath.Join(absDest, ".eggledger_probe")
		if err := os.MkdirAll(absDest, 0755); err != nil {
			return wrap(fmt.Errorf("cannot create destination: %w", err))
		}
		if err := os.WriteFile(probe, []byte("probe"), 0644); err != nil {
			return wrap(fmt.Errorf("destination not writable: %w", err))
		}
		os.Remove(probe)

		rollback := func() {
			if !destPreExisted {
				os.RemoveAll(absDest)
			}
		}

		if err := copyDir(absInternal, absDest); err != nil {
			rollback()
			return wrap(err)
		}

		if err := writeBootstrapConfig(absDest); err != nil {
			rollback()
			return wrap(err)
		}

		executable, err := os.Executable()
		if err != nil {
			return wrap(err)
		}
		cmd := exec.Command(executable, os.Args[1:]...)
		if err := cmd.Start(); err != nil {
			return wrap(err)
		}
		ui.Close()
		return nil
	})

	ui.MustBind("addAccount", func(eid string) (Account, error) {
		action := "addAccount"
		wrap := func(err error) error { return errors.Wrap(err, action) }
		ctx, cancel := context.WithTimeout(context.Background(), 20*time.Second)
		defer cancel()
		resp, err := fetchFirstContactWithContext(ctx, eid)
		if err != nil {
			return Account{}, wrap(err)
		}
		backup := resp.GetBackup()
		nickname := backup.GetUserName()
		eb := backup.GetEarningsBonus()
		roleColor, _, ebAddendum, eb, precision := RoleFromEB(eb)
		ebString := fmt.Sprintf(fmt.Sprintf("%%.%df", precision), eb) + ebAddendum
		game := backup.GetGame()
		seSuffix := AbbreviateFloat(game.GetSoulEggsD())
		peCount := int(game.GetEggsOfProphecy())
		totalTE := 0
		if virtue := backup.GetVirtue(); virtue != nil {
			for _, v := range virtue.GetEovEarned() {
				totalTE += int(v)
			}
		}
		acct := Account{
			Id:           eid,
			Nickname:     nickname,
			EBString:     ebString,
			AccountColor: roleColor,
			SeString:     seSuffix,
			PeCount:      peCount,
			TeCount:      totalTE,
		}
		_storage.AddKnownAccount(acct)
		_storage.Lock()
		_updateKnownAccounts(_storage.KnownAccounts)
		_storage.Unlock()
		return acct, nil
	})

	ui.MustBind("getActiveAccountId", func() string {
		return _storage.GetActiveAccountId()
	})

	ui.MustBind("setActiveAccountId", func(id string) {
		_storage.SetActiveAccountId(id)
	})

	ui.MustBind("openURL", func(url string) {
		if err := open.Start(url); err != nil {
			log.Errorf("opening %s: %s", url, err)
		}
	})

	ui.MustBind("isApiVersionStale", func() bool {
		return checkApiVersionStaleness()
	})

	ui.MustBind("getCompiledApiVersion", func() string {
		return api.AppVersion
	})

	ui.MustBind("checkForUpdates", func() []string {
		log.Info("checking for updates...")
		newVersion, newReleaseNotes, err := checkForUpdates()
		if err != nil {
			log.Error(err)
			return []string{"", ""}
		}
		switch newVersion {
		case "":
			log.Infof("no new version found")
			return []string{"", ""}
		case "skip":
			return []string{"", ""}
		default:
			log.Infof("new version found: %s", newVersion)
			return []string{newVersion, newReleaseNotes}
		}
	})

	ui.MustBind("isMennoRefreshNeeded", handleIsMennoRefreshNeeded)

	ui.MustBind("updateMennoData", func() {
		go handleUpdateMennoData(
			func(ok bool) { ui.Eval(fmt.Sprintf("window.onMennoRefreshDone(%t)", ok)) },
			_updateMennoDownloadProgress,
		)
	})

	ui.MustBind("secondsSinceLastMennoUpdate", handleSecondsSinceLastMennoUpdate)

	ui.MustBind("loadMennoData", handleLoadMennoData)

	ui.MustBind("getMennoData", handleGetMennoData)

	ui.MustBind("createReport", func(defJSON string) string {
		var def reports.ReportDefinition
		if err := json.Unmarshal([]byte(defJSON), &def); err != nil {
			log.Error(err)
			return ""
		}
		def.Weight = reports.ClassifyWeight(def)
		if def.Id == "" {
			def.Id = uuid.New().String()
		}
		row, err := reportDefToRow(def)
		if err != nil {
			log.Error(err)
			return ""
		}
		if err := reportdb.InsertReport(context.Background(), row); err != nil {
			log.Error(err)
			return ""
		}
		out, _ := json.Marshal(def)
		return string(out)
	})

	ui.MustBind("updateReport", func(defJSON string) bool {
		var def reports.ReportDefinition
		if err := json.Unmarshal([]byte(defJSON), &def); err != nil {
			log.Error(err)
			return false
		}
		def.Weight = reports.ClassifyWeight(def)
		row, err := reportDefToRow(def)
		if err != nil {
			log.Error(err)
			return false
		}
		if err := reportdb.UpdateReport(context.Background(), row); err != nil {
			log.Error(err)
			return false
		}
		return true
	})

	ui.MustBind("deleteReport", func(id string) bool {
		if err := reportdb.DeleteReport(context.Background(), id); err != nil {
			log.Error(err)
			return false
		}
		return true
	})

	ui.MustBind("getAccountReports", func(accountId string) string {
		rows, err := reportdb.RetrieveAccountReports(context.Background(), accountId)
		if err != nil {
			log.Error(err)
			return "[]"
		}
		defs := make([]reports.ReportDefinition, 0, len(rows))
		for _, r := range rows {
			def, err := rowToReportDef(r)
			if err != nil {
				log.Error(err)
				continue
			}
			defs = append(defs, def)
		}
		out, _ := json.Marshal(defs)
		return string(out)
	})

	ui.MustBind("executeReport", func(id string) string {
		row, err := reportdb.RetrieveReport(context.Background(), id)
		if err != nil {
			log.Error(err)
			return "{}"
		}
		def, err := rowToReportDef(*row)
		if err != nil {
			log.Error(err)
			return "{}"
		}
		result, err := reports.ExecuteReport(context.Background(), def)
		if err != nil {
			log.Error(err)
			return "{}"
		}
		out, _ := json.Marshal(result)
		return string(out)
	})

	ui.MustBind("reorderReports", func(idsJSON string) bool {
		var ids []string
		if err := json.Unmarshal([]byte(idsJSON), &ids); err != nil {
			log.Error(err)
			return false
		}
		if err := reportdb.ReorderReports(context.Background(), ids); err != nil {
			log.Error(err)
			return false
		}
		return true
	})

	ui.MustBind("exportReport", func(id string) string {
		wrap := func(err error) error { return errors.Wrap(err, "exportReport") }
		row, err := reportdb.RetrieveReport(context.Background(), id)
		if err != nil {
			log.Printf("%+v", wrap(err))
			return ""
		}
		def, err := rowToReportDef(*row)
		if err != nil {
			log.Printf("%+v", wrap(err))
			return ""
		}
		out := struct {
			ExportVersion string                   `json:"exportVersion"`
			ExportedAt    int64                    `json:"exportedAt"`
			Report        reports.ReportDefinition `json:"report"`
		}{
			ExportVersion: "1",
			ExportedAt:    time.Now().Unix(),
			Report:        def,
		}
		b, err := json.Marshal(out)
		if err != nil {
			log.Printf("%+v", wrap(err))
			return ""
		}
		return string(b)
	})

	ui.MustBind("exportAllReports", func(accountId string) string {
		wrap := func(err error) error { return errors.Wrap(err, "exportAllReports") }
		rows, err := reportdb.RetrieveAccountReports(context.Background(), accountId)
		if err != nil {
			log.Printf("%+v", wrap(err))
			return ""
		}
		defs := make([]reports.ReportDefinition, 0, len(rows))
		for _, row := range rows {
			def, defErr := rowToReportDef(row)
			if defErr != nil {
				log.Printf("%+v", wrap(defErr))
				return ""
			}
			defs = append(defs, def)
		}
		out := struct {
			ExportVersion string                     `json:"exportVersion"`
			ExportedAt    int64                      `json:"exportedAt"`
			AccountID     string                     `json:"accountId"`
			Reports       []reports.ReportDefinition `json:"reports"`
		}{
			ExportVersion: "1",
			ExportedAt:    time.Now().Unix(),
			AccountID:     accountId,
			Reports:       defs,
		}
		b, err := json.MarshalIndent(out, "", "  ")
		if err != nil {
			log.Printf("%+v", wrap(err))
			return ""
		}
		fname := fmt.Sprintf("reports-export-%d.json", time.Now().Unix())
		fpath := filepath.Join(_rootDir, fname)
		if err := os.WriteFile(fpath, b, 0644); err != nil {
			log.Printf("%+v", wrap(err))
			return ""
		}
		return fpath
	})

	ui.MustBind("importReport", func(accountId, jsonStr string) string {
		wrap := func(err error) error { return errors.Wrap(err, "importReport") }
		var payload struct {
			ExportVersion string                   `json:"exportVersion"`
			Report        reports.ReportDefinition `json:"report"`
		}
		if err := json.Unmarshal([]byte(jsonStr), &payload); err != nil {
			log.Printf("%+v", wrap(err))
			return ""
		}
		def := payload.Report
		def.Id = uuid.New().String()
		def.AccountId = accountId
		def.SortOrder = 0
		if def.Name == "" {
			def.Name = "Imported Report"
		}
		def.Weight = reports.ClassifyWeight(def)
		row, err := reportDefToRow(def)
		if err != nil {
			log.Printf("%+v", wrap(err))
			return ""
		}
		if err := reportdb.InsertReport(context.Background(), row); err != nil {
			log.Printf("%+v", wrap(err))
			return ""
		}
		return def.Id
	})

	ui.MustBind("getAccountGroups", func(accountId string) string {
		wrap := func(err error) error { return errors.Wrap(err, "getAccountGroups") }
		rows, err := reportdb.RetrieveAccountGroups(context.Background(), accountId)
		if err != nil {
			log.Printf("%+v", wrap(err))
			return "[]"
		}
		type groupOut struct {
			Id        string `json:"id"`
			AccountId string `json:"accountId"`
			Name      string `json:"name"`
			SortOrder int    `json:"sortOrder"`
			CreatedAt int64  `json:"createdAt"`
		}
		out := make([]groupOut, 0, len(rows))
		for _, r := range rows {
			out = append(out, groupOut{
				Id: r.Id, AccountId: r.AccountId, Name: r.Name,
				SortOrder: r.SortOrder, CreatedAt: r.CreatedAt,
			})
		}
		b, _ := json.Marshal(out)
		return string(b)
	})

	ui.MustBind("createReportGroup", func(accountId, name string) string {
		wrap := func(err error) error { return errors.Wrap(err, "createReportGroup") }
		row := reportdb.ReportGroupRow{AccountId: accountId, Name: name}
		id, err := reportdb.InsertReportGroup(context.Background(), row)
		if err != nil {
			log.Printf("%+v", wrap(err))
			return ""
		}
		return id
	})

	ui.MustBind("renameReportGroup", func(id, name string) bool {
		wrap := func(err error) error { return errors.Wrap(err, "renameReportGroup") }
		err := reportdb.UpdateReportGroup(context.Background(), reportdb.ReportGroupRow{Id: id, Name: name})
		if err != nil {
			log.Printf("%+v", wrap(err))
			return false
		}
		return true
	})

	ui.MustBind("deleteReportGroup", func(id string) bool {
		wrap := func(err error) error { return errors.Wrap(err, "deleteReportGroup") }
		if err := reportdb.DeleteReportGroup(context.Background(), id); err != nil {
			log.Printf("%+v", wrap(err))
			return false
		}
		return true
	})

	ui.MustBind("setReportGroup", func(reportId, groupId string) bool {
		wrap := func(err error) error { return errors.Wrap(err, "setReportGroup") }
		if err := reportdb.SetReportGroup(context.Background(), reportId, groupId); err != nil {
			log.Printf("%+v", wrap(err))
			return false
		}
		return true
	})

	ui.MustBind("exportGroupReports", func(groupId string) string {
		wrap := func(err error) error { return errors.Wrap(err, "exportGroupReports") }
		rows, err := reportdb.RetrieveReportsByGroup(context.Background(), groupId)
		if err != nil {
			log.Printf("%+v", wrap(err))
			return ""
		}
		group, err := reportdb.RetrieveReportGroup(context.Background(), groupId)
		if err != nil {
			log.Printf("%+v", wrap(err))
			return ""
		}
		defs := make([]reports.ReportDefinition, 0, len(rows))
		for _, row := range rows {
			def, defErr := rowToReportDef(row)
			if defErr != nil {
				log.Printf("%+v", wrap(defErr))
				return ""
			}
			defs = append(defs, def)
		}
		out := struct {
			ExportVersion string                     `json:"exportVersion"`
			ExportedAt    int64                      `json:"exportedAt"`
			GroupName     string                     `json:"groupName"`
			Reports       []reports.ReportDefinition `json:"reports"`
		}{
			ExportVersion: "1",
			ExportedAt:    time.Now().Unix(),
			GroupName:     group.Name,
			Reports:       defs,
		}
		b, err := json.Marshal(out)
		if err != nil {
			log.Printf("%+v", wrap(err))
			return ""
		}
		return string(b)
	})

	ui.MustBind("importGroupReports", func(accountId, jsonStr string) string {
		wrap := func(err error) error { return errors.Wrap(err, "importGroupReports") }
		var payload struct {
			ExportVersion string                     `json:"exportVersion"`
			GroupName     string                     `json:"groupName"`
			Reports       []reports.ReportDefinition `json:"reports"`
		}
		if err := json.Unmarshal([]byte(jsonStr), &payload); err != nil {
			log.Printf("%+v", wrap(err))
			return ""
		}
		groupRow := reportdb.ReportGroupRow{AccountId: accountId, Name: payload.GroupName}
		if groupRow.Name == "" {
			groupRow.Name = "Imported Group"
		}
		groupId, err := reportdb.InsertReportGroup(context.Background(), groupRow)
		if err != nil {
			log.Printf("%+v", wrap(err))
			return ""
		}
		for _, def := range payload.Reports {
			def.Id = uuid.New().String()
			def.AccountId = accountId
			def.GroupId = groupId
			def.SortOrder = 0
			if def.Name == "" {
				def.Name = "Imported Report"
			}
			def.Weight = reports.ClassifyWeight(def)
			row, rowErr := reportDefToRow(def)
			if rowErr != nil {
				log.Printf("%+v", wrap(rowErr))
				continue
			}
			if insErr := reportdb.InsertReport(context.Background(), row); insErr != nil {
				log.Printf("%+v", wrap(insErr))
			}
		}
		return groupId
	})

	ui.MustBind("getReportBackfillStatus", func() string {
		out, _ := json.Marshal(GetBackfillStatus())
		return string(out)
	})

	ui.MustBind("checkCloudReachable", handleCheckCloudReachable)

	ui.MustBind("getCloudSyncStatus", func() string {
		out, _ := json.Marshal(handleGetCloudSyncStatus())
		return string(out)
	})

	ui.MustBind("connectDiscord", func() (string, error) {
		return handleConnectDiscord()
	})

	ui.MustBind("disconnectCloud", handleDisconnectCloud)

	ui.MustBind("syncToCloud", func() {
		handleSyncToCloud()
	})

	ui.MustBind("restoreFromCloud", func() {
		handleRestoreFromCloud()
	})

	ln, err := net.Listen("tcp", "127.0.0.1:0")
	if err != nil {
		log.Fatal("tcp err: ", err)
	}
	defer ln.Close()
	go func() {
		var httpfs http.FileSystem
		if _devMode {
			httpfs = http.Dir("www/dist")
		} else {
			wwwfs, err := fs.Sub(_fs, "www/dist")
			if err != nil {
				log.Fatal("wwwfs err: ", err)
			}
			httpfs = http.FS(wwwfs)
		}
		err := http.Serve(ln, http.FileServer(httpfs))
		if err != nil {
			log.Fatal("httpServe err: ", err)
		}
	}()
	ui.MustLoad(fmt.Sprintf("http://%s/", ln.Addr()))

	// Wait until the interrupt signal arrives or browser window is closed.
	sigc := make(chan os.Signal, 1)
	signal.Notify(sigc, os.Interrupt)
	select {
	case <-sigc:
	case <-ui.Done():
	}

	// Make sure to cleanly close the database before exiting
	if err := db.CloseDB(); err != nil {
		log.Fatalf("Failed to close database: %v", err)
	}
}

// buildAppIconPath scales the embedded icon-64.png to 32x32 (matching
// LR_DEFAULTSIZE's SM_CXICON request), wraps it as a PNG-in-ICO file on disk,
// and returns its path so lorca can apply it to a Firefox window via
// WM_SETICON.  Returns an empty string on any failure (lorca falls back to
// PE resource 1 or skips icon setup on non-Windows platforms).
func buildAppIconPath() string {
	pngData, err := fs.ReadFile(_fs, "www/dist/images/icon-64.png")
	if err != nil {
		return ""
	}

	// Decode the source PNG.
	src, err := png.Decode(bytes.NewReader(pngData))
	if err != nil {
		return ""
	}
	srcBounds := src.Bounds()

	// Scale to 32x32 via center-pixel sampling so the ICO entry matches the
	// size that LoadImageW requests when LR_DEFAULTSIZE is set (SM_CXICON = 32).
	const dstW, dstH = 32, 32
	dst := image.NewNRGBA(image.Rect(0, 0, dstW, dstH))
	scaleX := srcBounds.Dx() / dstW
	scaleY := srcBounds.Dy() / dstH
	if scaleX < 1 {
		scaleX = 1
	}
	if scaleY < 1 {
		scaleY = 1
	}
	for dy := 0; dy < dstH; dy++ {
		for dx := 0; dx < dstW; dx++ {
			sx := srcBounds.Min.X + dx*scaleX + scaleX/2
			sy := srcBounds.Min.Y + dy*scaleY + scaleY/2
			dst.Set(dx, dy, src.At(sx, sy))
		}
	}

	// Re-encode as PNG.
	var scaledBuf bytes.Buffer
	if err := png.Encode(&scaledBuf, dst); err != nil {
		return ""
	}
	scaledPNG := scaledBuf.Bytes()

	// PNG-in-ICO layout: 6-byte ICONDIR + 16-byte ICONDIRENTRY + PNG bytes.
	const hdrSize = 22
	ico := make([]byte, hdrSize+len(scaledPNG))

	// ICONDIR
	ico[2] = 1 // idType = ICO
	ico[4] = 1 // idCount = 1

	// ICONDIRENTRY
	ico[6] = dstW // bWidth
	ico[7] = dstH // bHeight
	// bColorCount = 0, bReserved = 0
	ico[10] = 1  // wPlanes (uint16 LE) = 1
	ico[12] = 32 // wBitCount (uint16 LE) = 32
	sz := uint32(len(scaledPNG))
	ico[14] = byte(sz)
	ico[15] = byte(sz >> 8)
	ico[16] = byte(sz >> 16)
	ico[17] = byte(sz >> 24)
	ico[18] = hdrSize // dwImageOffset (low byte; upper 3 bytes already 0)

	copy(ico[hdrSize:], scaledPNG)

	icoPath := filepath.Join(_internalDir, "icon.ico")
	if err := os.WriteFile(icoPath, ico, 0644); err != nil {
		return ""
	}
	return icoPath
}
