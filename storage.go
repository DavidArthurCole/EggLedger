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

	KnownAccounts              []Account `json:"known_accounts"`
	ActiveAccountId            string    `json:"active_account_id"`
	LastMennoDataRefreshAt     time.Time `json:"last_menno_data_refresh_at"`
	LastUpdateCheckAt          time.Time `json:"last_update_check_at"`
	KnownLatestReleaseNotes    string    `json:"known_latest_release_notes"`
	KnownLatestVersion         string    `json:"known_latest_version"`
	FilterWarningRead          bool      `json:"filter_warning_read"`
	WorkerCountWarningRead     bool      `json:"worker_count_warning_read"`
	PreferredChromiumPath      string    `json:"preferred_chromium_path"`
	AutoRefreshMennoPref       bool      `json:"auto_refresh_menno_pref"`
	DefaultResolutionX         int       `json:"default_resolution_x"`
	DefaultResolutionY         int       `json:"default_resolution_y"`
	DefaultScalingFactor       float64   `json:"default_scaling_factor"`
	StartInFullscreen          bool      `json:"start_in_fullscreen"`
	RetryFailedMissions        bool      `json:"retry_failed_missions"`
	HideTimeoutErrors          bool      `json:"hide_timeout_errors"`
	WorkerCount                int       `json:"worker_count"`
	ScreenshotSafety           bool      `json:"screenshot_safety"`
	ShowMissionProgress        bool      `json:"show_mission_progress"`
	CollapseOlderSections      bool      `json:"collapse_older_sections"`
	AdvancedDropFilter         bool      `json:"advanced_drop_filter"`
	MissionViewByDate          bool      `json:"mission_view_by_date"`
	MissionViewTimes           bool      `json:"mission_view_times"`
	MissionRecolorDC           bool      `json:"mission_recolor_dc"`
	MissionRecolorBC           bool      `json:"mission_recolor_bc"`
	MissionShowExpectedDrops   bool      `json:"mission_show_expected_drops"`
	MissionMultiViewMode       string    `json:"mission_multi_view_mode"`
	MissionSortMethod          string    `json:"mission_sort_method"`
	LifetimeSortMethod         string    `json:"lifetime_sort_method"`
	LifetimeShowDropsPerShip   bool      `json:"lifetime_show_drops_per_ship"`
	LifetimeShowExpectedTotals bool      `json:"lifetime_show_expected_totals"`
	lastKnownGoodApiVersion    string

	CloudSessionToken      string    `json:"cloud_session_token"`
	CloudLastPushAt        time.Time `json:"cloud_last_push_at"`
	CloudLastPullAt        time.Time `json:"cloud_last_pull_at"`
	CloudDiscordUsername   string    `json:"cloud_discord_username"`
	CloudDiscordAvatarURL  string    `json:"cloud_discord_avatar_url"`
}

type Account struct {
	Id           string `json:"id"`
	Nickname     string `json:"nickname"`
	EBString     string `json:"ebString"`
	AccountColor string `json:"accountColor"`
	SeString     string `json:"seString"`
	PeCount      int    `json:"peCount"`
	TeCount      int    `json:"teCount"`
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
	if v, ok := settings["active_account_id"]; ok {
		s.ActiveAccountId = v
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
	if v, ok := settings["show_mission_progress"]; ok {
		s.ShowMissionProgress, _ = strconv.ParseBool(v)
	} else {
		s.ShowMissionProgress = true
	}
	if v, ok := settings["collapse_older_sections"]; ok {
		s.CollapseOlderSections, _ = strconv.ParseBool(v)
	} else {
		s.CollapseOlderSections = true
	}
	if v, ok := settings["advanced_drop_filter"]; ok {
		s.AdvancedDropFilter, _ = strconv.ParseBool(v)
	}
	if v, ok := settings["mission_view_by_date"]; ok {
		s.MissionViewByDate, _ = strconv.ParseBool(v)
	}
	if v, ok := settings["mission_view_times"]; ok {
		s.MissionViewTimes, _ = strconv.ParseBool(v)
	} else {
		s.MissionViewTimes = true
	}
	if v, ok := settings["mission_recolor_dc"]; ok {
		s.MissionRecolorDC, _ = strconv.ParseBool(v)
	}
	if v, ok := settings["mission_recolor_bc"]; ok {
		s.MissionRecolorBC, _ = strconv.ParseBool(v)
	}
	if v, ok := settings["mission_show_expected_drops"]; ok {
		s.MissionShowExpectedDrops, _ = strconv.ParseBool(v)
	} else {
		s.MissionShowExpectedDrops = true
	}
	if v, ok := settings["mission_multi_view_mode"]; ok && v != "" {
		s.MissionMultiViewMode = v
	} else {
		s.MissionMultiViewMode = "off"
	}
	if v, ok := settings["mission_sort_method"]; ok && v != "" {
		s.MissionSortMethod = v
	} else {
		s.MissionSortMethod = "default"
	}
	if v, ok := settings["lifetime_sort_method"]; ok && v != "" {
		s.LifetimeSortMethod = v
	} else {
		s.LifetimeSortMethod = "default"
	}
	if v, ok := settings["lifetime_show_drops_per_ship"]; ok {
		s.LifetimeShowDropsPerShip, _ = strconv.ParseBool(v)
	}
	if v, ok := settings["lifetime_show_expected_totals"]; ok {
		s.LifetimeShowExpectedTotals, _ = strconv.ParseBool(v)
	}
	if v, ok := settings["last_known_good_api_version"]; ok {
		s.lastKnownGoodApiVersion = v
	}
	if v, ok := settings["cloud_session_token"]; ok {
		s.CloudSessionToken = v
	}
	if v, ok := settings["cloud_last_push_at"]; ok && v != "" {
		if t, err := time.Parse(time.RFC3339Nano, v); err == nil {
			s.CloudLastPushAt = t
		}
	}
	if v, ok := settings["cloud_last_pull_at"]; ok && v != "" {
		if t, err := time.Parse(time.RFC3339Nano, v); err == nil {
			s.CloudLastPullAt = t
		}
	}
	if v, ok := settings["cloud_discord_username"]; ok {
		s.CloudDiscordUsername = v
	}
	if v, ok := settings["cloud_discord_avatar_url"]; ok {
		s.CloudDiscordAvatarURL = v
	}
}

// persistAllToDB writes every field to the settings table in one transaction.
// Used only during the one-time JSON migration on first run.
func (s *AppStorage) persistAllToDB() {
	s.Lock()
	accountsJSON, _ := json.Marshal(s.KnownAccounts)
	settings := map[string]string{
		"known_accounts":                string(accountsJSON),
		"active_account_id":             s.ActiveAccountId,
		"last_menno_data_refresh_at":    s.LastMennoDataRefreshAt.Format(time.RFC3339Nano),
		"last_update_check_at":          s.LastUpdateCheckAt.Format(time.RFC3339Nano),
		"known_latest_release_notes":    s.KnownLatestReleaseNotes,
		"known_latest_version":          s.KnownLatestVersion,
		"filter_warning_read":           strconv.FormatBool(s.FilterWarningRead),
		"worker_count_warning_read":     strconv.FormatBool(s.WorkerCountWarningRead),
		"preferred_chromium_path":       s.PreferredChromiumPath,
		"auto_refresh_menno_pref":       strconv.FormatBool(s.AutoRefreshMennoPref),
		"default_resolution_x":          strconv.Itoa(s.DefaultResolutionX),
		"default_resolution_y":          strconv.Itoa(s.DefaultResolutionY),
		"default_scaling_factor":        strconv.FormatFloat(s.DefaultScalingFactor, 'f', -1, 64),
		"start_in_fullscreen":           strconv.FormatBool(s.StartInFullscreen),
		"retry_failed_missions":         strconv.FormatBool(s.RetryFailedMissions),
		"hide_timeout_errors":           strconv.FormatBool(s.HideTimeoutErrors),
		"worker_count":                  strconv.Itoa(s.WorkerCount),
		"screenshot_safety":             strconv.FormatBool(s.ScreenshotSafety),
		"show_mission_progress":         strconv.FormatBool(s.ShowMissionProgress),
		"collapse_older_sections":       strconv.FormatBool(s.CollapseOlderSections),
		"advanced_drop_filter":          strconv.FormatBool(s.AdvancedDropFilter),
		"mission_view_by_date":          strconv.FormatBool(s.MissionViewByDate),
		"mission_view_times":            strconv.FormatBool(s.MissionViewTimes),
		"mission_recolor_dc":            strconv.FormatBool(s.MissionRecolorDC),
		"mission_recolor_bc":            strconv.FormatBool(s.MissionRecolorBC),
		"mission_show_expected_drops":   strconv.FormatBool(s.MissionShowExpectedDrops),
		"mission_multi_view_mode":       s.MissionMultiViewMode,
		"mission_sort_method":           s.MissionSortMethod,
		"lifetime_sort_method":          s.LifetimeSortMethod,
		"lifetime_show_drops_per_ship":  strconv.FormatBool(s.LifetimeShowDropsPerShip),
		"lifetime_show_expected_totals": strconv.FormatBool(s.LifetimeShowExpectedTotals),
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

func (s *AppStorage) SetShowMissionProgress(flag bool) {
	s.Lock()
	s.ShowMissionProgress = flag
	s.Unlock()
	go s.dbSet("show_mission_progress", strconv.FormatBool(flag))
}

func (s *AppStorage) GetShowMissionProgress() bool {
	s.Lock()
	defer s.Unlock()
	return s.ShowMissionProgress
}

func (s *AppStorage) SetCollapseOlderSections(flag bool) {
	s.Lock()
	s.CollapseOlderSections = flag
	s.Unlock()
	go s.dbSet("collapse_older_sections", strconv.FormatBool(flag))
}

func (s *AppStorage) GetCollapseOlderSections() bool {
	s.Lock()
	defer s.Unlock()
	return s.CollapseOlderSections
}

func (s *AppStorage) SetAdvancedDropFilter(flag bool) {
	s.Lock()
	s.AdvancedDropFilter = flag
	s.Unlock()
	go s.dbSet("advanced_drop_filter", strconv.FormatBool(flag))
}

func (s *AppStorage) GetAdvancedDropFilter() bool {
	s.Lock()
	defer s.Unlock()
	return s.AdvancedDropFilter
}

func (s *AppStorage) GetLastKnownGoodApiVersion() string {
	s.Lock()
	defer s.Unlock()
	return s.lastKnownGoodApiVersion
}

func (s *AppStorage) SetLastKnownGoodApiVersion(v string) {
	s.Lock()
	s.lastKnownGoodApiVersion = v
	s.Unlock()
	go s.dbSet("last_known_good_api_version", v)
}

func (s *AppStorage) SetMissionViewByDate(flag bool) {
	s.Lock()
	s.MissionViewByDate = flag
	s.Unlock()
	go s.dbSet("mission_view_by_date", strconv.FormatBool(flag))
}

func (s *AppStorage) GetMissionViewByDate() bool {
	s.Lock()
	defer s.Unlock()
	return s.MissionViewByDate
}

func (s *AppStorage) SetMissionViewTimes(flag bool) {
	s.Lock()
	s.MissionViewTimes = flag
	s.Unlock()
	go s.dbSet("mission_view_times", strconv.FormatBool(flag))
}

func (s *AppStorage) GetMissionViewTimes() bool {
	s.Lock()
	defer s.Unlock()
	return s.MissionViewTimes
}

func (s *AppStorage) SetMissionRecolorDC(flag bool) {
	s.Lock()
	s.MissionRecolorDC = flag
	s.Unlock()
	go s.dbSet("mission_recolor_dc", strconv.FormatBool(flag))
}

func (s *AppStorage) GetMissionRecolorDC() bool {
	s.Lock()
	defer s.Unlock()
	return s.MissionRecolorDC
}

func (s *AppStorage) SetMissionRecolorBC(flag bool) {
	s.Lock()
	s.MissionRecolorBC = flag
	s.Unlock()
	go s.dbSet("mission_recolor_bc", strconv.FormatBool(flag))
}

func (s *AppStorage) GetMissionRecolorBC() bool {
	s.Lock()
	defer s.Unlock()
	return s.MissionRecolorBC
}

func (s *AppStorage) SetMissionShowExpectedDrops(flag bool) {
	s.Lock()
	s.MissionShowExpectedDrops = flag
	s.Unlock()
	go s.dbSet("mission_show_expected_drops", strconv.FormatBool(flag))
}

func (s *AppStorage) GetMissionShowExpectedDrops() bool {
	s.Lock()
	defer s.Unlock()
	return s.MissionShowExpectedDrops
}

func (s *AppStorage) SetMissionMultiViewMode(mode string) {
	s.Lock()
	s.MissionMultiViewMode = mode
	s.Unlock()
	go s.dbSet("mission_multi_view_mode", mode)
}

func (s *AppStorage) GetMissionMultiViewMode() string {
	s.Lock()
	defer s.Unlock()
	if s.MissionMultiViewMode == "" {
		return "off"
	}
	return s.MissionMultiViewMode
}

func (s *AppStorage) GetMissionSortMethod() string {
	s.Lock()
	defer s.Unlock()
	if s.MissionSortMethod == "" {
		return "default"
	}
	return s.MissionSortMethod
}

func (s *AppStorage) SetMissionSortMethod(mode string) {
	s.Lock()
	s.MissionSortMethod = mode
	s.Unlock()
	go s.dbSet("mission_sort_method", mode)
}

func (s *AppStorage) SetLifetimeSortMethod(mode string) {
	s.Lock()
	s.LifetimeSortMethod = mode
	s.Unlock()
	go s.dbSet("lifetime_sort_method", mode)
}

func (s *AppStorage) GetLifetimeSortMethod() string {
	s.Lock()
	defer s.Unlock()
	if s.LifetimeSortMethod == "" {
		return "default"
	}
	return s.LifetimeSortMethod
}

func (s *AppStorage) SetLifetimeShowDropsPerShip(flag bool) {
	s.Lock()
	s.LifetimeShowDropsPerShip = flag
	s.Unlock()
	go s.dbSet("lifetime_show_drops_per_ship", strconv.FormatBool(flag))
}

func (s *AppStorage) GetLifetimeShowDropsPerShip() bool {
	s.Lock()
	defer s.Unlock()
	return s.LifetimeShowDropsPerShip
}

func (s *AppStorage) SetLifetimeShowExpectedTotals(flag bool) {
	s.Lock()
	s.LifetimeShowExpectedTotals = flag
	s.Unlock()
	go s.dbSet("lifetime_show_expected_totals", strconv.FormatBool(flag))
}

func (s *AppStorage) GetLifetimeShowExpectedTotals() bool {
	s.Lock()
	defer s.Unlock()
	return s.LifetimeShowExpectedTotals
}

func (s *AppStorage) GetActiveAccountId() string {
	s.Lock()
	defer s.Unlock()
	return s.ActiveAccountId
}

func (s *AppStorage) SetActiveAccountId(id string) {
	s.Lock()
	s.ActiveAccountId = id
	s.Unlock()
	go s.dbSet("active_account_id", id)
}

func (s *AppStorage) GetCloudSessionToken() string {
	s.Lock()
	defer s.Unlock()
	return s.CloudSessionToken
}

func (s *AppStorage) SetCloudSessionToken(token string) {
	s.Lock()
	s.CloudSessionToken = token
	s.Unlock()
	go s.dbSet("cloud_session_token", token)
}

func (s *AppStorage) GetCloudLastPushAt() time.Time {
	s.Lock()
	defer s.Unlock()
	return s.CloudLastPushAt
}

func (s *AppStorage) SetCloudLastPushAt(t time.Time) {
	s.Lock()
	s.CloudLastPushAt = t
	s.Unlock()
	go s.dbSet("cloud_last_push_at", t.Format(time.RFC3339Nano))
}

func (s *AppStorage) GetCloudLastPullAt() time.Time {
	s.Lock()
	defer s.Unlock()
	return s.CloudLastPullAt
}

func (s *AppStorage) SetCloudLastPullAt(t time.Time) {
	s.Lock()
	s.CloudLastPullAt = t
	s.Unlock()
	go s.dbSet("cloud_last_pull_at", t.Format(time.RFC3339Nano))
}

func (s *AppStorage) GetCloudDiscordUsername() string {
	s.Lock()
	defer s.Unlock()
	return s.CloudDiscordUsername
}

func (s *AppStorage) SetCloudDiscordUsername(username string) {
	s.Lock()
	s.CloudDiscordUsername = username
	s.Unlock()
	go s.dbSet("cloud_discord_username", username)
}

func (s *AppStorage) GetCloudDiscordAvatarURL() string {
	s.Lock()
	defer s.Unlock()
	return s.CloudDiscordAvatarURL
}

func (s *AppStorage) SetCloudDiscordAvatarURL(url string) {
	s.Lock()
	s.CloudDiscordAvatarURL = url
	s.Unlock()
	go s.dbSet("cloud_discord_avatar_url", url)
}
