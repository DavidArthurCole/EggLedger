package main

import (
	"context"
	"encoding/json"
	"os"
	"path/filepath"
	"strconv"
	"sync"
	"time"

	"github.com/DavidArthurCole/EggLedger/db"
	log "github.com/sirupsen/logrus"
)

type AppStorage struct {
	sync.Mutex

	KnownAccounts           []Account `json:"known_accounts"`
	LastMennoDataRefreshAt  time.Time `json:"last_menno_data_refresh_at"`
	LastUpdateCheckAt       time.Time `json:"last_update_check_at"`
	KnownLatestReleaseNotes string    `json:"known_latest_release_notes"`
	KnownLatestVersion      string    `json:"known_latest_version"`
	FilterWarningRead       bool      `json:"filter_warning_read"`
	WorkerCountWarningRead  bool      `json:"worker_count_warning_read"`
	PreferredChromiumPath   string    `json:"preferred_chromium_path"`
	AutoRefreshMennoPref    bool      `json:"auto_refresh_menno_pref"`
	DefaultViewMode         string    `json:"default_view_mode"`
	DefaultResolutionX      int       `json:"default_resolution_x"`
	DefaultResolutionY      int       `json:"default_resolution_y"`
	DefaultScalingFactor    float64   `json:"default_scaling_factor"`
	StartInFullscreen       bool      `json:"start_in_fullscreen"`
	RetryFailedMissions     bool      `json:"retry_failed_missions"`
	HideTimeoutErrors       bool      `json:"hide_timeout_errors"`
	WorkerCount             int       `json:"worker_count"`
	ScreenshotSafety        bool      `json:"screenshot_safety"`
}

type Account struct {
	Id           string `json:"id"`
	Nickname     string `json:"nickname"`
	EBString     string `json:"ebString"`
	AccountColor string `json:"accountColor"`
	SeString     string `json:"seString"`
	PeCount      int    `json:"peCount"`
	EotCount     int    `json:"eotCount"`
}

var _storage AppStorage

// storageInit must be called after dataInit (DB must already be open).
// If internal/storage.json exists it is loaded, migrated to DB in one batch,
// then renamed to storage.json.migrated so it is never processed again.
func storageInit() {
	storageFile := filepath.Join(_internalDir, "storage.json")
	if _, err := os.Stat(storageFile); err == nil {
		if _storage.loadFromJSONFile(storageFile) {
			_storage.persistAllToDB()
			if renameErr := os.Rename(storageFile, storageFile+".migrated"); renameErr != nil {
				log.Warnf("storage: could not rename storage.json after migration: %s", renameErr)
			}
		}
		_storage.loadFromDB()
	} else {
		_storage.loadFromDB()
	}
}

// dbSet writes a single key-value pair to the settings table.
// Intended to be called inside a goroutine (fire-and-forget write).
func (s *AppStorage) dbSet(key, value string) {
	if err := db.SetSetting(context.Background(), key, value); err != nil {
		log.Errorf("storage: failed to write setting %q: %s", key, err)
	}
}

// loadFromJSONFile reads an existing storage.json into the in-memory struct.
// Used only during the one-time migration path.
// Returns true if the file was successfully read and parsed, false otherwise.
func (s *AppStorage) loadFromJSONFile(path string) bool {
	s.Lock()
	defer s.Unlock()
	encoded, err := os.ReadFile(path)
	if err != nil {
		if !os.IsNotExist(err) {
			log.Errorf("storage: error loading %s: %s", path, err)
		}
		return false
	}
	if err := json.Unmarshal(encoded, s); err != nil {
		log.Errorf("storage: error parsing %s: %s", path, err)
		return false
	}
	return true
}

// loadFromDB populates the in-memory struct from the settings table.
func (s *AppStorage) loadFromDB() {
	s.Lock()
	defer s.Unlock()
	settings, err := db.GetAllSettings(context.Background())
	if err != nil {
		log.Errorf("storage: failed to load settings from DB: %s", err)
		return
	}
	if v, ok := settings["known_accounts"]; ok && v != "" {
		if err := json.Unmarshal([]byte(v), &s.KnownAccounts); err != nil {
			log.Warnf("storage: failed to parse known_accounts: %s", err)
		}
	}
	if v, ok := settings["last_menno_data_refresh_at"]; ok && v != "" {
		if t, err := time.Parse(time.RFC3339Nano, v); err == nil {
			s.LastMennoDataRefreshAt = t
		}
	}
	if v, ok := settings["last_update_check_at"]; ok && v != "" {
		if t, err := time.Parse(time.RFC3339Nano, v); err == nil {
			s.LastUpdateCheckAt = t
		}
	}
	if v, ok := settings["known_latest_release_notes"]; ok {
		s.KnownLatestReleaseNotes = v
	}
	if v, ok := settings["known_latest_version"]; ok {
		s.KnownLatestVersion = v
	}
	if v, ok := settings["filter_warning_read"]; ok {
		s.FilterWarningRead, _ = strconv.ParseBool(v)
	}
	if v, ok := settings["worker_count_warning_read"]; ok {
		s.WorkerCountWarningRead, _ = strconv.ParseBool(v)
	}
	if v, ok := settings["preferred_chromium_path"]; ok {
		s.PreferredChromiumPath = v
	}
	if v, ok := settings["auto_refresh_menno_pref"]; ok {
		s.AutoRefreshMennoPref, _ = strconv.ParseBool(v)
	}
	if v, ok := settings["default_view_mode"]; ok {
		s.DefaultViewMode = v
	}
	if v, ok := settings["default_resolution_x"]; ok {
		s.DefaultResolutionX, _ = strconv.Atoi(v)
	}
	if v, ok := settings["default_resolution_y"]; ok {
		s.DefaultResolutionY, _ = strconv.Atoi(v)
	}
	if v, ok := settings["default_scaling_factor"]; ok {
		s.DefaultScalingFactor, _ = strconv.ParseFloat(v, 64)
	}
	if v, ok := settings["start_in_fullscreen"]; ok {
		s.StartInFullscreen, _ = strconv.ParseBool(v)
	}
	if v, ok := settings["retry_failed_missions"]; ok {
		s.RetryFailedMissions, _ = strconv.ParseBool(v)
	}
	if v, ok := settings["hide_timeout_errors"]; ok {
		s.HideTimeoutErrors, _ = strconv.ParseBool(v)
	}
	if v, ok := settings["worker_count"]; ok {
		if n, err := strconv.Atoi(v); err == nil {
			s.WorkerCount = n
		}
	}
	if v, ok := settings["screenshot_safety"]; ok {
		s.ScreenshotSafety, _ = strconv.ParseBool(v)
	}
}

// persistAllToDB writes every field to the settings table in one transaction.
// Used only during the one-time JSON migration on first run.
func (s *AppStorage) persistAllToDB() {
	s.Lock()
	accountsJSON, _ := json.Marshal(s.KnownAccounts)
	settings := map[string]string{
		"known_accounts":             string(accountsJSON),
		"last_menno_data_refresh_at": s.LastMennoDataRefreshAt.Format(time.RFC3339Nano),
		"last_update_check_at":       s.LastUpdateCheckAt.Format(time.RFC3339Nano),
		"known_latest_release_notes": s.KnownLatestReleaseNotes,
		"known_latest_version":       s.KnownLatestVersion,
		"filter_warning_read":        strconv.FormatBool(s.FilterWarningRead),
		"worker_count_warning_read":  strconv.FormatBool(s.WorkerCountWarningRead),
		"preferred_chromium_path":    s.PreferredChromiumPath,
		"auto_refresh_menno_pref":    strconv.FormatBool(s.AutoRefreshMennoPref),
		"default_view_mode":          s.DefaultViewMode,
		"default_resolution_x":       strconv.Itoa(s.DefaultResolutionX),
		"default_resolution_y":       strconv.Itoa(s.DefaultResolutionY),
		"default_scaling_factor":     strconv.FormatFloat(s.DefaultScalingFactor, 'f', -1, 64),
		"start_in_fullscreen":        strconv.FormatBool(s.StartInFullscreen),
		"retry_failed_missions":      strconv.FormatBool(s.RetryFailedMissions),
		"hide_timeout_errors":        strconv.FormatBool(s.HideTimeoutErrors),
		"worker_count":               strconv.Itoa(s.WorkerCount),
		"screenshot_safety":          strconv.FormatBool(s.ScreenshotSafety),
	}
	s.Unlock()
	if err := db.SetSettings(context.Background(), settings); err != nil {
		log.Errorf("storage: failed to persist all settings to DB: %s", err)
	}
}

func (s *AppStorage) AddKnownAccount(account Account) {
	s.Lock()
	accounts := []Account{account}
	seen := map[string]struct{}{account.Id: {}}
	for _, a := range s.KnownAccounts {
		if _, exists := seen[a.Id]; !exists {
			accounts = append(accounts, a)
			seen[a.Id] = struct{}{}
		}
	}
	s.KnownAccounts = accounts
	accountsJSON, _ := json.Marshal(accounts)
	s.Unlock()
	go s.dbSet("known_accounts", string(accountsJSON))
}

func (s *AppStorage) SetUpdateCheck(latestVersion string, latestReleaseNotes string) {
	now := time.Now()
	s.Lock()
	s.LastUpdateCheckAt = now
	s.KnownLatestVersion = latestVersion
	s.KnownLatestReleaseNotes = latestReleaseNotes
	s.Unlock()
	go func() {
		s.dbSet("last_update_check_at", now.Format(time.RFC3339Nano))
		s.dbSet("known_latest_version", latestVersion)
		s.dbSet("known_latest_release_notes", latestReleaseNotes)
	}()
}

func (s *AppStorage) SetFilterWarningRead(flag bool) {
	s.Lock()
	s.FilterWarningRead = flag
	s.Unlock()
	go s.dbSet("filter_warning_read", strconv.FormatBool(flag))
}

func (s *AppStorage) SetWorkerCountWarningRead(flag bool) {
	s.Lock()
	s.WorkerCountWarningRead = flag
	s.Unlock()
	go s.dbSet("worker_count_warning_read", strconv.FormatBool(flag))
}

func (s *AppStorage) SetLastMennoDataRefreshAt(t time.Time) {
	s.Lock()
	s.LastMennoDataRefreshAt = t
	s.Unlock()
	go s.dbSet("last_menno_data_refresh_at", t.Format(time.RFC3339Nano))
}

func (s *AppStorage) SetPreferredChromiumPath(path string) {
	s.Lock()
	s.PreferredChromiumPath = path
	s.Unlock()
	go s.dbSet("preferred_chromium_path", path)
}

func (s *AppStorage) SetAutoRefreshMennoPref(flag bool) {
	s.Lock()
	s.AutoRefreshMennoPref = flag
	s.Unlock()
	go s.dbSet("auto_refresh_menno_pref", strconv.FormatBool(flag))
}

func (s *AppStorage) SetDefaultViewMode(mode string) {
	s.Lock()
	s.DefaultViewMode = mode
	s.Unlock()
	go s.dbSet("default_view_mode", mode)
}

func (s *AppStorage) SetDefaultResolution(x, y int) {
	s.Lock()
	if x < 650 || x > 3840 {
		x = 650
	}
	if y < 650 || y > 2160 {
		y = 650
	}
	s.DefaultResolutionX = x
	s.DefaultResolutionY = y
	s.Unlock()
	go func() {
		s.dbSet("default_resolution_x", strconv.Itoa(x))
		s.dbSet("default_resolution_y", strconv.Itoa(y))
	}()
}

func (s *AppStorage) GetDefaultResolution() []int {
	s.Lock()
	defer s.Unlock()
	defx := s.DefaultResolutionX
	defy := s.DefaultResolutionY
	if defx == 0 || defy == 0 {
		return []int{650, 650}
	}
	if defx < 650 || defx > 3840 {
		defx = 650
	}
	if defy < 650 || defy > 2160 {
		defy = 650
	}
	return []int{defx, defy}
}

func (s *AppStorage) SetDefaultScalingFactor(factor float64) {
	s.Lock()
	if factor < 0.5 || factor > 2.0 {
		factor = 1.0
	}
	s.DefaultScalingFactor = factor
	s.Unlock()
	go s.dbSet("default_scaling_factor", strconv.FormatFloat(factor, 'f', -1, 64))
}

func (s *AppStorage) GetDefaultScalingFactor() float64 {
	s.Lock()
	defer s.Unlock()
	factor := s.DefaultScalingFactor
	if factor < 0.5 || factor > 2.0 {
		factor = 1.0
	}
	return factor
}

func (s *AppStorage) SetStartInFullscreen(flag bool) {
	s.Lock()
	s.StartInFullscreen = flag
	s.Unlock()
	go s.dbSet("start_in_fullscreen", strconv.FormatBool(flag))
}

func (s *AppStorage) SetRetryFailedMissions(flag bool) {
	s.Lock()
	s.RetryFailedMissions = flag
	s.Unlock()
	go s.dbSet("retry_failed_missions", strconv.FormatBool(flag))
}

func (s *AppStorage) GetRetryFailedMissions() bool {
	s.Lock()
	defer s.Unlock()
	return s.RetryFailedMissions
}

func (s *AppStorage) SetHideTimeoutErrors(flag bool) {
	s.Lock()
	s.HideTimeoutErrors = flag
	s.Unlock()
	go s.dbSet("hide_timeout_errors", strconv.FormatBool(flag))
}

func (s *AppStorage) GetHideTimeoutErrors() bool {
	s.Lock()
	defer s.Unlock()
	return s.HideTimeoutErrors
}

func (s *AppStorage) GetWorkerCount() int {
	s.Lock()
	defer s.Unlock()
	n := s.WorkerCount
	if n < 1 {
		return 1
	}
	if n > 10 {
		return 10
	}
	return n
}

func (s *AppStorage) SetWorkerCount(n int) {
	if n < 1 {
		n = 1
	}
	if n > 10 {
		n = 10
	}
	s.Lock()
	s.WorkerCount = n
	s.Unlock()
	go s.dbSet("worker_count", strconv.Itoa(n))
}

func (s *AppStorage) SetScreenshotSafety(flag bool) {
	s.Lock()
	s.ScreenshotSafety = flag
	s.Unlock()
	go s.dbSet("screenshot_safety", strconv.FormatBool(flag))
}

func (s *AppStorage) GetScreenshotSafety() bool {
	s.Lock()
	defer s.Unlock()
	return s.ScreenshotSafety
}
