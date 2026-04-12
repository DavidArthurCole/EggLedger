package main

import (
	"context"
	"embed"
	"encoding/json"
	"flag"
	"fmt"
	"io/fs"
	"net"
	"net/http"
	"os"
	"os/signal"
	"path/filepath"
	"regexp"
	"runtime"
	"strings"
	"time"

	"github.com/DavidArthurCole/EggLedger/db"
	"github.com/DavidArthurCole/EggLedger/ei"
	"github.com/DavidArthurCole/EggLedger/eiafx"
	"github.com/DavidArthurCole/EggLedger/platform"
	"github.com/davidarthurcole/lorca"
	log "github.com/sirupsen/logrus"
	"github.com/sirupsen/logrus/hooks/writer"
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
	_eiAfxConfigErr        = eiafx.LoadConfig()
	_eiAfxConfigMissions   []*ei.ArtifactsConfigurationResponse_MissionParameters
	_eiAfxConfigArtis      []*ei.ArtifactsConfigurationResponse_ArtifactParameters
	_nominalShipCapacities = map[ei.MissionInfo_Spaceship]map[ei.MissionInfo_DurationType][]float32{}
	_latestMennoData       = MennoData{}
	_possibleTargets       = []PossibleTarget{}
	_possibleArtifacts     = []PossibleArtifact{}

	_forceMennoRefresh bool
	_forceUpdateCheck  bool

	_processRegistry             *ProcessRegistry
	_updateKnownAccounts         func([]Account)
	_updateState                 func(AppState)
	_updateMissionProgress       func(MissionProgress)
	_updateMennoDownloadProgress func(MennoDownloadProgress)
	_updateExportedFiles         func([]string)
	_emitMessage                 func(string, bool)
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

type RawPossibleTarget struct {
	Name        ei.ArtifactSpec_Name `json:"name"`
	DisplayName string               `json:"displayName"`
	ImageString string               `json:"imageString"`
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

	_internalDir = filepath.Join(_rootDir, "internal")

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

	if _eiAfxConfigErr != nil {
		log.Fatal("_eiAfxConfigErr: ", _eiAfxConfigErr)
	} else {
		_eiAfxConfigMissions = eiafx.Config.MissionParameters
		_eiAfxConfigArtis = eiafx.Config.ArtifactParameters
		initNominalShipCapacities()
	}

	dataInit()
	storageInit()
	initPossibleTargets()
	initPossibleArtifacts()
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

	if len(_nominalShipCapacities) == 0 {
		initNominalShipCapacities()
	}

	//Get list of complete missions from the DB
	completeMissions, err := db.RetrievePlayerCompleteMissions(ctx, eid)
	if err != nil {
		log.Error(err)
		return nil, err
	}
	//Array of LoadedMission
	missionArr := []DatabaseMission{}

	for _, completeMission := range completeMissions {
		missionArr = append(missionArr, compileMissionInformation(completeMission))
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
	PossibleTargetsRaw := []RawPossibleTarget{
		{Name: ei.ArtifactSpec_UNKNOWN, DisplayName: "Untargeted", ImageString: "none.png"},
		{Name: ei.ArtifactSpec_BOOK_OF_BASAN, DisplayName: "Books of Basan", ImageString: "bob_target.png"},
		{Name: ei.ArtifactSpec_TACHYON_DEFLECTOR, DisplayName: "Tachyon Deflectors", ImageString: "deflector_target.png"},
		{Name: ei.ArtifactSpec_SHIP_IN_A_BOTTLE, DisplayName: "Ships in a Bottle", ImageString: "siab_target.png"},
		{Name: ei.ArtifactSpec_TITANIUM_ACTUATOR, DisplayName: "Titanium Actuators", ImageString: "actuator_target.png"},
		{Name: ei.ArtifactSpec_DILITHIUM_MONOCLE, DisplayName: "Dilithium Monocles", ImageString: "monocle_target.png"},
		{Name: ei.ArtifactSpec_QUANTUM_METRONOME, DisplayName: "Quantum Metronomes", ImageString: "metronome_target.png"},
		{Name: ei.ArtifactSpec_PHOENIX_FEATHER, DisplayName: "Phoenix Feathers", ImageString: "feather_target.png"},
		{Name: ei.ArtifactSpec_THE_CHALICE, DisplayName: "Chalices", ImageString: "chalice_target.png"},
		{Name: ei.ArtifactSpec_INTERSTELLAR_COMPASS, DisplayName: "Interstellar Compasses", ImageString: "compass_target.png"},
		{Name: ei.ArtifactSpec_CARVED_RAINSTICK, DisplayName: "Carved Rainsticks", ImageString: "rainstick_target.png"},
		{Name: ei.ArtifactSpec_BEAK_OF_MIDAS, DisplayName: "Beaks of Midas", ImageString: "beak_target.png"},
		{Name: ei.ArtifactSpec_MERCURYS_LENS, DisplayName: "Mercury's Lenses", ImageString: "lens_target.png"},
		{Name: ei.ArtifactSpec_NEODYMIUM_MEDALLION, DisplayName: "Neodymium Medallions", ImageString: "medallion_target.png"},
		{Name: ei.ArtifactSpec_ORNATE_GUSSET, DisplayName: "Gussets", ImageString: "gusset_target.png"},
		{Name: ei.ArtifactSpec_TUNGSTEN_ANKH, DisplayName: "Tungsten Ankhs", ImageString: "ankh_target.png"},
		{Name: ei.ArtifactSpec_AURELIAN_BROOCH, DisplayName: "Aurelian Brooches", ImageString: "brooch_target.png"},
		{Name: ei.ArtifactSpec_VIAL_MARTIAN_DUST, DisplayName: "Vials of Martian Dust", ImageString: "vial_target.png"},
		{Name: ei.ArtifactSpec_DEMETERS_NECKLACE, DisplayName: "Demeters Necklaces", ImageString: "necklace_target.png"},
		{Name: ei.ArtifactSpec_LUNAR_TOTEM, DisplayName: "Lunar Totems", ImageString: "totem_target.png"},
		{Name: ei.ArtifactSpec_PUZZLE_CUBE, DisplayName: "Puzzle Cubes", ImageString: "cube_target.png"},
		{Name: ei.ArtifactSpec_PROPHECY_STONE, DisplayName: "Prophecy Stones", ImageString: "prophecy_target.png"},
		{Name: ei.ArtifactSpec_CLARITY_STONE, DisplayName: "Clarity Stones", ImageString: "clarity_target.png"},
		{Name: ei.ArtifactSpec_DILITHIUM_STONE, DisplayName: "Dilithium Stones", ImageString: "dilithium_target.png"},
		{Name: ei.ArtifactSpec_LIFE_STONE, DisplayName: "Life Stones", ImageString: "life_target.png"},
		{Name: ei.ArtifactSpec_QUANTUM_STONE, DisplayName: "Quantum Stones", ImageString: "quantum_target.png"},
		{Name: ei.ArtifactSpec_SOUL_STONE, DisplayName: "Soul Stones", ImageString: "soul_target.png"},
		{Name: ei.ArtifactSpec_TERRA_STONE, DisplayName: "Terra Stones", ImageString: "terra_target.png"},
		{Name: ei.ArtifactSpec_TACHYON_STONE, DisplayName: "Tachyon Stones", ImageString: "tachyon_target.png"},
		{Name: ei.ArtifactSpec_LUNAR_STONE, DisplayName: "Lunar Stones", ImageString: "lunar_target.png"},
		{Name: ei.ArtifactSpec_SHELL_STONE, DisplayName: "Shell Stones", ImageString: "shell_target.png"},
		{Name: ei.ArtifactSpec_SOLAR_TITANIUM, DisplayName: "Solar Titanium", ImageString: "titanium_target.png"},
		{Name: ei.ArtifactSpec_TAU_CETI_GEODE, DisplayName: "Geodes", ImageString: "geode_target.png"},
		{Name: ei.ArtifactSpec_GOLD_METEORITE, DisplayName: "Gold Meteorites", ImageString: "gold_target.png"},
		{Name: ei.ArtifactSpec_PROPHECY_STONE_FRAGMENT, DisplayName: "Prophecy Stone Fragments", ImageString: "prophecy_frag_target.png"},
		{Name: ei.ArtifactSpec_CLARITY_STONE_FRAGMENT, DisplayName: "Clarity Stone Fragments", ImageString: "clarity_frag_target.png"},
		{Name: ei.ArtifactSpec_LIFE_STONE_FRAGMENT, DisplayName: "Life Stone Fragments", ImageString: "life_frag_target.png"},
		{Name: ei.ArtifactSpec_TERRA_STONE_FRAGMENT, DisplayName: "Terra Stone Fragments", ImageString: "terra_frag_target.png"},
		{Name: ei.ArtifactSpec_DILITHIUM_STONE_FRAGMENT, DisplayName: "Dilithium Stone Fragments", ImageString: "dilithium_frag_target.png"},
		{Name: ei.ArtifactSpec_SOUL_STONE_FRAGMENT, DisplayName: "Soul Stone Fragments", ImageString: "soul_frag_target.png"},
		{Name: ei.ArtifactSpec_QUANTUM_STONE_FRAGMENT, DisplayName: "Quantum Stone Fragments", ImageString: "quantum_frag_target.png"},
		{Name: ei.ArtifactSpec_TACHYON_STONE_FRAGMENT, DisplayName: "Tachyon Stone Fragments", ImageString: "tachyon_frag_target.png"},
		{Name: ei.ArtifactSpec_SHELL_STONE_FRAGMENT, DisplayName: "Shell Stone Fragments", ImageString: "shell_frag_target.png"},
		{Name: ei.ArtifactSpec_LUNAR_STONE_FRAGMENT, DisplayName: "Lunar Stone Fragments", ImageString: "lunar_frag_target.png"},
	}

	// Convert the array to PossibleTarget
	possibleTargets := []PossibleTarget{
		{DisplayName: "None (Pre 1.27)", Id: -1, ImageString: "none.png"},
	}
	for _, rawTarget := range PossibleTargetsRaw {
		possibleTarget := PossibleTarget{
			DisplayName: rawTarget.DisplayName,
			Id:          int32(rawTarget.Name),
			ImageString: rawTarget.ImageString,
		}
		possibleTargets = append(possibleTargets, possibleTarget)
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

func main() {
	flag.BoolVar(&_forceMennoRefresh, "force-menno-refresh", false, "treat menno data as stale regardless of last refresh time")
	flag.BoolVar(&_forceUpdateCheck, "force-update-check", false, "bypass the 12-hour update-check cooldown")
	flag.Parse()

	if _devMode {
		log.Info("starting app in dev mode")
	}

	prefBrowserPath := _storage.PreferredChromiumPath
	browser := lorca.LocateChrome(prefBrowserPath)
	if prefBrowserPath != browser {
		_storage.SetPreferredChromiumPath("")
	}
	if browser == "" {
		lorca.PromptDownload()
		log.Fatal("unable to locate a supported browser")
		return
	}

	// Firefox support is temporarily disabled. If LocateChrome fell back to
	// Firefox (no Chromium-family browser installed), treat it as not found.
	if strings.Contains(strings.ToLower(browser), "firefox") {
		browser = ""
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
	u, err := lorca.New("", "", browser, widthPreference, heightPreference, args...)
	if err != nil {
		log.Fatal("lorca err: ", err)
	}
	ui := UI{u}
	defer ui.Close()

	_updateKnownAccounts = func(accounts []Account) {
		encoded, err := json.Marshal(accounts)
		if err != nil {
			log.Error(err)
			return
		}
		ui.Eval(fmt.Sprintf("window.updateKnownAccounts(%s)", encoded))
	}
	_updateState = func(state AppState) {
		ui.Eval(fmt.Sprintf("window.updateState('%s')", state))
	}
	_updateMissionProgress = func(progress MissionProgress) {
		encoded, err := json.Marshal(progress)
		if err != nil {
			log.Error(err)
			return
		}
		ui.Eval(fmt.Sprintf("window.updateMissionProgress(%s)", encoded))
	}
	_updateMennoDownloadProgress = func(progress MennoDownloadProgress) {
		encoded, err := json.Marshal(progress)
		if err != nil {
			log.Error(err)
			return
		}
		ui.Eval(fmt.Sprintf("window.updateMennoDownloadProgress(%s)", encoded))
	}
	_updateExportedFiles = func(files []string) {
		encoded, err := json.Marshal(files)
		if err != nil {
			log.Error(err)
			return
		}
		ui.Eval(fmt.Sprintf("window.updateExportedFiles(%s)", encoded))
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
		encoded, err := json.Marshal(snapshots)
		if err != nil {
			log.Error(err)
			return
		}
		ui.Eval(fmt.Sprintf("window.updateProcesses(%s)", encoded))
	}
	_processRegistry = NewProcessRegistry(updateProcesses)
	_processRegistry.Start(context.Background())

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

	ui.MustBind("getMaxQuality", func() float32 {
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
	})

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

	ui.MustBind("getWorkerCount", func() int {
		return _storage.GetWorkerCount()
	})

	ui.MustBind("setWorkerCount", func(n int) {
		_storage.SetWorkerCount(n)
	})

	ui.MustBind("getAfxConfigs", func() []PossibleArtifact {
		if len(_possibleArtifacts) == 0 {
			initPossibleArtifacts()
		}

		return _possibleArtifacts
	})

	ui.MustBind("getPossibleTargets", func() []PossibleTarget {
		if len(_possibleTargets) == 0 {
			initPossibleArtifacts()
		}

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

	ui.MustBind("openURL", func(url string) {
		if err := open.Start(url); err != nil {
			log.Errorf("opening %s: %s", url, err)
		}
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

	ui.MustBind("getDefaultViewMode", func() string {
		_storage.Lock()
		viewMode := _storage.DefaultViewMode
		_storage.Unlock()
		if len(viewMode) == 0 {
			viewMode = "default"
		}
		return viewMode
	})

	ui.MustBind("setDefaultViewMode", func(viewMode string) {
		_storage.SetDefaultViewMode(viewMode)
	})

	ui.MustBind("loadMennoData", handleLoadMennoData)

	ui.MustBind("getMennoData", handleGetMennoData)

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
