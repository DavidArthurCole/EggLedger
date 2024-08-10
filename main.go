package main

import (
	"context"
	"embed"
	"encoding/json"
	"fmt"
	"io/fs"
	"math"
	"net"
	"net/http"
	"os"
	"os/signal"
	"path/filepath"
	"regexp"
	"runtime"
	"strings"
	"sync"
	"time"

	"github.com/DavidArthurCole/EggLedger/db"
	"github.com/DavidArthurCole/EggLedger/ei"
	"github.com/DavidArthurCole/EggLedger/eiafx"
	"github.com/davidarthurcole/lorca"
	humanize "github.com/dustin/go-humanize"
	"github.com/pkg/errors"
	log "github.com/sirupsen/logrus"
	"github.com/sirupsen/logrus/hooks/writer"
	"github.com/skratchdot/open-golang/open"
	"golang.org/x/sync/semaphore"
	"gopkg.in/natefinch/lumberjack.v2"
)

var (
	//go:embed VERSION
	_appVersion string

	//go:embed www
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
	AppState_AWAITING_INPUT    AppState = "AwaitingInput"
	AppState_FETCHING_SAVE     AppState = "FetchingSave"
	AppState_FETCHING_MISSIONS AppState = "FetchingMissions"
	AppState_EXPORTING_DATA    AppState = "ExportingData"
	AppState_SUCCESS           AppState = "Success"
	AppState_FAILED            AppState = "Failed"
	AppState_INTERRUPTED       AppState = "Interrupted"
)

type MissionProgress struct {
	Total                   int     `json:"total"`
	Finished                int     `json:"finished"`
	FinishedPercentage      string  `json:"finishedPercentage"`
	ExpectedFinishTimestamp float64 `json:"expectedFinishTimestamp"`
}

type worker struct {
	*semaphore.Weighted
	ctx     context.Context
	cancel  context.CancelFunc
	ctxlock sync.Mutex
}

type ExportedFile struct {
	File     string `json:"file"`
	Count    int    `json:"count"`
	DateTime string `json:"datetime"`
	EID      string `json:"eid"`
}

type MissionDrop struct {
	Id           int32   `json:"id"`
	SpecType     string  `json:"specType"`
	Name         string  `json:"name"`
	GameName     string  `json:"gameName"`
	EffectString string  `json:"effectString"`
	Level        int32   `json:"level"`
	Rarity       int32   `json:"rarity"`
	Quality      float64 `json:"quality"`
	IVOrder      int32   `json:"ivOrder"`
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

type PossibleMission struct {
	Ship      *ei.MissionInfo_Spaceship `json:"ship"`
	Durations []*DurationConfig         `json:"durations"`
}

type DurationConfig struct {
	DurationType     *ei.MissionInfo_DurationType `json:"durationType"`
	MinQuality       float64                      `json:"minQuality"`
	MaxQuality       float64                      `json:"maxQuality"`
	LevelQualityBump float64                      `json:"levelQualityBump"`
	MaxLevels        int32                        `json:"maxLevels"`
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
	if err := hide(_internalDir); err != nil {
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

	storageInit()
	dataInit()
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
	if _devMode {
		log.Info("starting app in dev mode")
	}

	prefChromePath := _storage.PreferredChromiumPath
	chrome := lorca.LocateChrome(prefChromePath)
	if prefChromePath != chrome {
		_storage.SetPreferredChromiumPath("")
	}
	if chrome == "" {
		lorca.PromptDownload()
		log.Fatal("unable to locate Chrome")
		return
	}

	args := []string{}
	args = append(args, "--disable-features=TranslateUI,BlinkGenPropertyTrees")
	/* User preference args */
	scalingFactor := _storage.GetDefaultScalingFactor()
	if scalingFactor != 1.0 {
		args = append(args, "--force-device-scale-factor="+fmt.Sprintf("%f", scalingFactor))
	}
	if _storage.StartInFullscreen {
		args = append(args, "--start-fullscreen")
	}
	if runtime.GOOS == "linux" {
		args = append(args, "--class=Lorca")
	}
	widthPreference, heightPreference := func() (int, int) {
		resolutionPrefs := _storage.GetDefaultResolution()
		return resolutionPrefs[0], resolutionPrefs[1]
	}()
	u, err := lorca.New("", "", chrome, widthPreference, heightPreference, args...)
	if err != nil {
		log.Fatal("lorca err: ", err)
	}
	ui := UI{u}
	defer ui.Close()

	updateKnownAccounts := func(accounts []Account) {
		encoded, err := json.Marshal(accounts)
		if err != nil {
			log.Error(err)
			return
		}
		ui.Eval(fmt.Sprintf("window.updateKnownAccounts(%s)", encoded))
	}
	updateState := func(state AppState) {
		ui.Eval(fmt.Sprintf("window.updateState('%s')", state))
	}
	updateMissionProgress := func(progress MissionProgress) {
		encoded, err := json.Marshal(progress)
		if err != nil {
			log.Error(err)
			return
		}
		ui.Eval(fmt.Sprintf("window.updateMissionProgress(%s)", encoded))
	}
	updateExportedFiles := func(files []string) {
		encoded, err := json.Marshal(files)
		if err != nil {
			log.Error(err)
			return
		}
		ui.Eval(fmt.Sprintf("window.updateExportedFiles(%s)", encoded))
	}
	emitMessage := func(message string, isError bool) {
		encoded, err := json.Marshal(message)
		if err != nil {
			log.Error(err)
			return
		}
		ui.Eval(fmt.Sprintf("window.emitMessage(%s, %t)", encoded, isError))
	}

	pinfo := func(args ...interface{}) {
		log.Info(args...)
		emitMessage(fmt.Sprint(args...), false)
	}
	perror := func(args ...interface{}) {
		log.Error(args...)
		emitMessage(fmt.Sprint(args...), true)
	}

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

	w := &worker{
		Weighted: semaphore.NewWeighted(1),
	}
	ui.MustBind("fetchPlayerData", func(playerId string) {
		go func() {
			if !w.TryAcquire(1) {
				perror("already fetching player data, cannot accept new work")
				return
			}
			defer w.Release(1)

			ctx, cancel := context.WithCancel(context.Background())
			w.ctxlock.Lock()
			w.ctx = ctx
			w.cancel = cancel
			w.ctxlock.Unlock()

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
			fc, err := fetchFirstContactWithContext(w.ctx, playerId)
			if err != nil {
				perror(err)
				if !checkInterrupt() {
					updateState(AppState_FAILED)
				}
				return
			}

			backup := fc.GetBackup()
			game := backup.GetGame()
			nickname := backup.GetUserName()
			//EB calculations
			soulEggBonus := 10.0
			prophecyEggBonus := 1.05
			for _, er := range game.GetEpicResearch() {
				if strings.ToLower(er.GetId()) == "soul_eggs" {
					soulEggBonus = float64(er.GetLevel()) + 10
				} else if strings.ToLower(er.GetId()) == "prophecy_bonus" {
					prophecyEggBonus = (float64(er.GetLevel())+5)/100 + 1
				}
			}
			eb := float64(game.GetSoulEggsD() * soulEggBonus * math.Pow(float64(prophecyEggBonus), float64(game.GetEggsOfProphecy())))
			roleColor, roleString, ebAddendum, eb, precision := RoleFromEB(eb)
			ebString := fmt.Sprintf(fmt.Sprintf("%%.%df", precision), eb) + ebAddendum

			msg := fmt.Sprintf("successfully fetched backup for &7a7a7a<%s>", playerId)
			if nickname != "" {
				msg += fmt.Sprintf(" (&%s<%s>)", roleColor, nickname)
			}
			pinfo(msg)

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
			_storage.AddKnownAccount(Account{Id: playerId, Nickname: nickname, EBString: ebString, AccountColor: roleColor})
			_storage.Lock()
			updateKnownAccounts(_storage.KnownAccounts)
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

			total := len(newMissionIds)
			finished := 0
			if total > 0 {
				updateState(AppState_FETCHING_MISSIONS)

				reportProgress := func(finished int) {
					updateMissionProgress(MissionProgress{
						Total:                   total,
						Finished:                finished,
						FinishedPercentage:      fmt.Sprintf("%.1f%%", float64(finished)/float64(total)*100),
						ExpectedFinishTimestamp: timeToUnix(time.Now().Add(time.Duration(total-finished) * _requestInterval)),
					})
				}

				reportProgress(finished)

				finishedCh := make(chan struct{}, total)
				go func() {
					for range finishedCh {
						finished++
						reportProgress(finished)
					}
				}()

				errored := 0
				var wg sync.WaitGroup
				type FailedMission struct {
					missionId      string
					startTimestamp float64
				}
				failedMissions := []FailedMission{}
				tryFailedAgain := _storage.GetRetryFailedMissions()

			MissionsLoop:
				for i := 0; i < total; i++ {
					if i != 0 {
						select {
						case <-ctx.Done():
							break MissionsLoop
						case <-time.After(_requestInterval):
						}
					}
					wg.Add(1)
					go func(missionId string, startTimestamp float64) {
						defer wg.Done()
						_, err := fetchCompleteMissionWithContext(w.ctx, playerId, missionId, startTimestamp)
						if err != nil {
							perror(err)
							errored++
							if tryFailedAgain {
								failedMissions = append(failedMissions, FailedMission{missionId, startTimestamp})
							}
						}
						finishedCh <- struct{}{}
					}(newMissionIds[i], newMissionStartTimestamps[i])
				}
				wg.Wait()

				if checkInterrupt() {
					return
				}

				if errored > 0 {
					perror(fmt.Sprintf("%d of %d missions failed to fetch", errored, total))

					if tryFailedAgain {
						errored = 0
						pinfo("retrying failed missions")

						// Update the total to include the number of failed missions
						total += len(failedMissions)

						for _, failedMission := range failedMissions {
							wg.Add(1)
							go func(missionId string, startTimestamp float64) {
								defer wg.Done()
								_, err := fetchCompleteMissionWithContext(w.ctx, playerId, missionId, startTimestamp)
								if err != nil {
									perror(err)
									errored++
								}
								finishedCh <- struct{}{}
							}(failedMission.missionId, failedMission.startTimestamp)
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
				}
			}

			updateState(AppState_EXPORTING_DATA)
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
				log.Info("exports unchanged, using last exported files and deleting new ones")
				emitMessage("exports identical with existing data files, reusing", false)
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
			updateExportedFiles([]string{strings.TrimSpace(xlsxFileRel), strings.TrimSpace(csvFileRel)})

			pinfo("done.")
			updateState(AppState_SUCCESS)
		}()
	})

	ui.MustBind("stopFetchingPlayerData", func() {
		w.ctxlock.Lock()
		defer w.ctxlock.Unlock()
		if w.cancel != nil {
			w.cancel()
		}
	})

	ui.MustBind("getExistingData", func() []DatabaseAccount {
		knownAccounts := []DatabaseAccount{}
		for _, knownAccount := range _storage.KnownAccounts {
			ids, err := db.RetrievePlayerCompleteMissionIds(context.Background(), knownAccount.Id)
			if err != nil {
				log.Error(err)
			} else if len(ids) > 0 {
				knownAccounts = append(knownAccounts,
					DatabaseAccount{
						Id:           knownAccount.Id,
						Nickname:     knownAccount.Nickname,
						MissionCount: len(ids),
						EBString:     knownAccount.EBString,
						AccountColor: knownAccount.AccountColor,
					},
				)
			}
		}
		return knownAccounts
	})

	ui.MustBind("getMissionIds", func(playerId string) []string {
		ids, err := db.RetrievePlayerCompleteMissionIds(context.Background(), playerId)
		if err != nil {
			log.Error(err)
			return nil
		}
		return ids
	})

	ui.MustBind("viewMissionsOfEid", func(eid string) []DatabaseMission {
		if dbMissions, err := viewMissionsOfId(context.Background(), eid); err != nil {
			log.Error(err)
			return nil
		} else {
			return dbMissions
		}
	})

	ui.MustBind("getDurationConfigs", func() []PossibleMission {
		possibleMissions := []PossibleMission{}

		for _, mission := range _eiAfxConfigMissions {
			ship := mission.Ship
			durations := []*DurationConfig{}
			for _, duration := range mission.GetDurations() {
				durationConfig := &DurationConfig{
					DurationType:     duration.DurationType,
					MinQuality:       float64(duration.GetMinQuality()),
					MaxQuality:       float64(duration.GetMaxQuality()),
					LevelQualityBump: float64(duration.GetLevelQualityBump()),
					MaxLevels:        int32(len(mission.LevelMissionRequirements)),
				}
				durations = append(durations, durationConfig)
			}
			possibleMission := PossibleMission{
				Ship:      ship,
				Durations: durations,
			}
			possibleMissions = append(possibleMissions, possibleMission)
		}

		return possibleMissions
	})

	ui.MustBind("getShipDrops", func(playerId string, shipId string) []MissionDrop {
		//Get the mission from the database
		completeMission, err := db.RetrieveCompleteMission(context.Background(), playerId, shipId)
		if err != nil {
			log.Error(err)
			return nil
		}

		shipDrops := []MissionDrop{} //Array of drops
		for _, drop := range completeMission.Artifacts {
			spec := drop.GetSpec()
			var foundQuality float64 = 0
			// Iterate through the array to find the desired ArtifactParameters
			for _, artifact := range _eiAfxConfigArtis {
				// Compare the Spec field
				if artifact.Spec == spec {
					// Match found, retrieve the BaseQuality
					foundQuality = *artifact.BaseQuality
					break
				}
			}
			missionDrop := MissionDrop{
				Id:       int32(spec.GetName()),
				Name:     ei.ArtifactSpec_Name_name[int32(spec.GetName())],
				GameName: spec.CasedName(),
				Level:    int32(*drop.Spec.Level),
				Rarity:   int32(*drop.Spec.Rarity),
				Quality:  foundQuality,
				IVOrder:  int32(spec.Name.InventoryVisualizerOrder()),
			}
			switch {
			case strings.Contains(missionDrop.Name, "_FRAGMENT"):
				missionDrop.SpecType = "StoneFragment"
			case strings.Contains(missionDrop.Name, "_STONE"):
				missionDrop.SpecType = "Stone"
				missionDrop.EffectString = spec.DropEffectString()
			case strings.Contains(missionDrop.Name, "GOLD_METEORITE"),
				strings.Contains(missionDrop.Name, "SOLAR_TITANIUM"),
				strings.Contains(missionDrop.Name, "TAU_CETI_GEODE"):
				missionDrop.SpecType = "Ingredient"
			default:
				missionDrop.SpecType = "Artifact"
				missionDrop.EffectString = spec.DropEffectString()
			}
			shipDrops = append(shipDrops, missionDrop)
		}

		return shipDrops
	})

	ui.MustBind("getMissionInfo", func(playerId string, missionId string) DatabaseMission {
		return getMissionInformation(context.Background(), playerId, missionId)
	})

	ui.MustBind("openFile", func(file string) {
		path := filepath.Join(_rootDir, file)
		if err := open.Start(path); err != nil {
			log.Errorf("opening %s: %s", path, err)
		}
	})

	ui.MustBind("openFileInFolder", func(file string) {
		path := filepath.Join(_rootDir, file)
		if err := openFolderAndSelect(path); err != nil {
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
		if newVersion == "" {
			log.Infof("no new version found")
			return []string{"", ""}
		} else if newVersion == "skip" {
			return []string{"", ""}
		} else {
			log.Infof("new version found: %s", newVersion)
			return []string{newVersion, newReleaseNotes}
		}
	})

	ui.MustBind("isMennoRefreshNeeded", func() bool {
		return checkIfRefreshMennoDataIsNeeded()
	})

	ui.MustBind("updateMennoData", func() bool {
		err := refreshMennoData()
		if err != nil {
			return false
		} else {
			return true
		}
	})

	ui.MustBind("secondsSinceLastMennoUpdate", func() int {
		_storage.Lock()
		lastRefresh := _storage.LastMennoDataRefreshAt
		_storage.Unlock()
		if lastRefresh.IsZero() {
			return math.MaxInt32
		} else {
			return int(time.Since(lastRefresh).Seconds())
		}
	})

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

	ui.MustBind("loadMennoData", func() bool {
		_latestMennoData, err = loadLatestMennoData()
		if err != nil {
			if !strings.Contains(err.Error(), "no menno data available") {
				log.Error(err)
			}
			return false
		}
		return true
	})

	ui.MustBind("getMennoData", func(ship int, shipDuration int, shipLevel int, targetArtifact int) []ConfigurationItem {
		// If the data is not loaded, return an empty MennoData
		if len(_latestMennoData.ConfigurationItems) == 0 {
			_latestMennoData, err = loadLatestMennoData()
			if err != nil || len(_latestMennoData.ConfigurationItems) == 0 {
				log.Error(err)
				return nil
			}
		}

		filteredMennoData := MennoData{}
		for _, configurationItem := range _latestMennoData.ConfigurationItems {
			sc := configurationItem.ShipConfiguration
			if sc.ShipType.Id == ship && sc.ShipDurationType.Id == shipDuration && sc.Level == shipLevel && sc.TargetArtifact.Id == targetArtifact {
				filteredMennoData.ConfigurationItems = append(filteredMennoData.ConfigurationItems, configurationItem)
			}
		}
		return filteredMennoData.ConfigurationItems
	})

	ln, err := net.Listen("tcp", "127.0.0.1:0")
	if err != nil {
		log.Fatal("tcp err: ", err)
	}
	defer ln.Close()
	go func() {
		var httpfs http.FileSystem
		if _devMode {
			httpfs = http.Dir("www")
		} else {
			wwwfs, err := fs.Sub(_fs, "www")
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
