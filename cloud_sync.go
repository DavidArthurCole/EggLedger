package main

// cloud_sync.go - Discord OAuth2 authentication and blob sync against
// https://ledgersync.davidarthurcole.me
//
// Blobs are AES-256-GCM encrypted client-side before upload. The encryption key
// is generated server-side on first auth and returned with every subsequent auth,
// so the same key is available on all devices after reconnecting.

import (
	"bytes"
	"context"
	"crypto/aes"
	"crypto/cipher"
	"crypto/rand"
	"encoding/base64"
	"encoding/hex"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"time"

	"github.com/DavidArthurCole/EggLedger/reportdb"
	"github.com/DavidArthurCole/EggLedger/reports"
	log "github.com/sirupsen/logrus"
	"github.com/skratchdot/open-golang/open"
)

const (
	_cloudSyncBaseURL      = "https://ledgersync.davidarthurcole.me/api/v1"
	_cloudSyncHTTPTimeout  = 10 * time.Second
	_cloudAuthPollTimeout  = 5 * time.Minute
	_cloudAuthPollInterval = 2 * time.Second
	_cloudReachableTimeout = 5 * time.Second
)

// CloudSyncStatus is returned to the UI by getCloudSyncStatus.
type CloudSyncStatus struct {
	Connected        bool   `json:"connected"`
	Username         string `json:"username"`
	AvatarURL        string `json:"avatarUrl"`
	LastPushAt       int64  `json:"lastPushAt"`
	LastPullAt       int64  `json:"lastPullAt"`
	HasEncryptionKey bool   `json:"hasEncryptionKey"`
}

// cloudSyncableSettings is the subset of AppStorage that is safe to sync
// across machines (excludes paths, resolution, and machine-specific prefs).
type cloudSyncableSettings struct {
	AutoRefreshMennoPref       bool   `json:"auto_refresh_menno_pref"`
	RetryFailedMissions        bool   `json:"retry_failed_missions"`
	HideTimeoutErrors          bool   `json:"hide_timeout_errors"`
	WorkerCount                int    `json:"worker_count"`
	ScreenshotSafety           bool   `json:"screenshot_safety"`
	ShowMissionProgress        bool   `json:"show_mission_progress"`
	CollapseOlderSections      bool   `json:"collapse_older_sections"`
	AdvancedDropFilter         bool   `json:"advanced_drop_filter"`
	MissionViewByDate          bool   `json:"mission_view_by_date"`
	MissionViewTimes           bool   `json:"mission_view_times"`
	MissionRecolorDC           bool   `json:"mission_recolor_dc"`
	MissionRecolorBC           bool   `json:"mission_recolor_bc"`
	MissionShowExpectedDrops   bool   `json:"mission_show_expected_drops"`
	MissionMultiViewMode       string `json:"mission_multi_view_mode"`
	MissionSortMethod          string `json:"mission_sort_method"`
	LifetimeSortMethod         string `json:"lifetime_sort_method"`
	LifetimeShowDropsPerShip   bool   `json:"lifetime_show_drops_per_ship"`
	LifetimeShowExpectedTotals bool   `json:"lifetime_show_expected_totals"`
}

// cloudReportsBlob is the structure stored in the "reports" blob.
type cloudReportsBlob struct {
	Reports []reports.ReportDefinition `json:"reports"`
	Groups  []reportdb.ReportGroupRow  `json:"groups"`
}

// blobEnvelope wraps the payload for PUT/GET requests, matching the server's ciphertext field.
type blobEnvelope struct {
	Ciphertext string `json:"ciphertext"`
}

// authInitResponse is returned by GET /auth/discord.
type authInitResponse struct {
	State string `json:"state"`
	URL   string `json:"url"`
}

// pollResponse is the shape of a successful GET /auth/poll response.
type pollResponse struct {
	Token         string `json:"token"`
	Username      string `json:"username"`
	AvatarURL     string `json:"avatarUrl"`
	EncryptionKey string `json:"encryptionKey"`
}

// Package-level Go-to-JS callbacks, wired in main() after lorca is ready.
var (
	_onDiscordAuthComplete  func(connected bool, username string)
	_onCloudSyncComplete    func(success bool, errMsg string)
	_onCloudRestoreComplete func(success bool, errMsg string)
)

// checkCloudSyncReachable probes /verify with a short timeout.
func checkCloudSyncReachable() bool {
	client := &http.Client{Timeout: _cloudReachableTimeout}
	resp, err := client.Get(_cloudSyncBaseURL + "/verify")
	if err != nil {
		log.Debugf("cloud sync: reachability probe failed: %v", err)
		return false
	}
	resp.Body.Close()
	return resp.StatusCode == http.StatusOK
}

// handleGetCloudSyncStatus returns the current cloud sync state.
func handleGetCloudSyncStatus() CloudSyncStatus {
	var lastPush, lastPull int64
	if t := _storage.GetCloudLastPushAt(); !t.IsZero() {
		lastPush = t.Unix()
	}
	if t := _storage.GetCloudLastPullAt(); !t.IsZero() {
		lastPull = t.Unix()
	}
	return CloudSyncStatus{
		Connected:        _storage.GetCloudSessionToken() != "",
		Username:         _storage.GetCloudDiscordUsername(),
		AvatarURL:        _storage.GetCloudDiscordAvatarURL(),
		LastPushAt:       lastPush,
		LastPullAt:       lastPull,
		HasEncryptionKey: _storage.GetCloudEncryptionKey() != "",
	}
}

// handleCheckCloudReachable performs the reachability probe synchronously.
func handleCheckCloudReachable() bool {
	return checkCloudSyncReachable()
}

// handleConnectDiscord starts the Discord OAuth2 polling flow. It asks the
// server for the auth URL and state, opens the URL in the system browser, then
// polls /auth/poll?state=<state> until the user authenticates or the 5-minute
// window expires. Fires _onDiscordAuthComplete when done. Returns the Discord
// authorization URL for the UI fallback link.
func handleConnectDiscord() (string, error) {
	client := &http.Client{Timeout: _cloudSyncHTTPTimeout}
	resp, err := client.Get(_cloudSyncBaseURL + "/auth/discord")
	if err != nil {
		return "", fmt.Errorf("connectDiscord: init: %w", err)
	}
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusOK {
		return "", fmt.Errorf("connectDiscord: server returned %d", resp.StatusCode)
	}
	var init authInitResponse
	if err := json.NewDecoder(resp.Body).Decode(&init); err != nil {
		return "", fmt.Errorf("connectDiscord: decode: %w", err)
	}

	if err := open.Start(init.URL); err != nil {
		log.Warnf("cloud sync: could not open browser: %v", err)
	}

	go func() {
		ctx, cancel := context.WithTimeout(context.Background(), _cloudAuthPollTimeout)
		defer cancel()

		ticker := time.NewTicker(_cloudAuthPollInterval)
		defer ticker.Stop()

		for {
			select {
			case <-ctx.Done():
				log.Warn("cloud sync: auth polling timed out")
				if _onDiscordAuthComplete != nil {
					_onDiscordAuthComplete(false, "")
				}
				return
			case <-ticker.C:
				token, username, avatarURL, encryptionKey, pollErr := pollAuthToken(ctx, init.State)
				if pollErr != nil {
					log.Debugf("cloud sync: poll error: %v", pollErr)
					continue
				}
				if token == "" {
					continue
				}
				_storage.SetCloudSessionToken(token)
				_storage.SetCloudEncryptionKey(encryptionKey)
				_storage.SetCloudDiscordUsername(username)
				_storage.SetCloudDiscordAvatarURL(avatarURL)
				log.Infof("cloud sync: authenticated as %s", username)
				if _onDiscordAuthComplete != nil {
					_onDiscordAuthComplete(true, username)
				}
				return
			}
		}
	}()

	return init.URL, nil
}

// pollAuthToken calls GET /auth/poll?state=<state> once.
// Returns ("", "", "", "", nil) while still pending, (token, username, avatarURL, encryptionKey, nil) on success,
// ("", "", "", "", err) on a definitive server error.
func pollAuthToken(ctx context.Context, state string) (string, string, string, string, error) {
	req, err := http.NewRequestWithContext(ctx, http.MethodGet,
		_cloudSyncBaseURL+"/auth/poll?state="+state, nil)
	if err != nil {
		return "", "", "", "", err
	}
	client := &http.Client{Timeout: _cloudSyncHTTPTimeout}
	resp, err := client.Do(req)
	if err != nil {
		return "", "", "", "", err
	}
	defer resp.Body.Close()

	switch resp.StatusCode {
	case http.StatusAccepted, http.StatusNotFound:
		return "", "", "", "", nil
	case http.StatusOK:
	default:
		return "", "", "", "", fmt.Errorf("poll: unexpected status %d", resp.StatusCode)
	}

	var pr pollResponse
	if err := json.NewDecoder(resp.Body).Decode(&pr); err != nil {
		return "", "", "", "", fmt.Errorf("poll: decode: %w", err)
	}
	return pr.Token, pr.Username, pr.AvatarURL, pr.EncryptionKey, nil
}

// handleDisconnectCloud invalidates the server-side session and clears local creds.
func handleDisconnectCloud() {
	token := _storage.GetCloudSessionToken()
	if token != "" {
		go func() {
			req, err := http.NewRequest(http.MethodDelete, _cloudSyncBaseURL+"/auth/session", nil)
			if err != nil {
				log.Warnf("cloud sync: disconnect: %v", err)
				return
			}
			req.Header.Set("Authorization", "Bearer "+token)
			client := &http.Client{Timeout: _cloudSyncHTTPTimeout}
			resp, err := client.Do(req)
			if err != nil {
				log.Warnf("cloud sync: disconnect: %v", err)
				return
			}
			resp.Body.Close()
		}()
	}
	_storage.SetCloudSessionToken("")
	_storage.SetCloudDiscordUsername("")
}

// handleSyncToCloud serializes and uploads all blobs. Fires _onCloudSyncComplete when done.
func handleSyncToCloud() {
	go func() {
		if err := runSyncToCloud(context.Background()); err != nil {
			log.Errorf("cloud sync: sync failed: %v", err)
			if _onCloudSyncComplete != nil {
				_onCloudSyncComplete(false, err.Error())
			}
			return
		}
		_storage.SetCloudLastPushAt(time.Now())
		if _onCloudSyncComplete != nil {
			_onCloudSyncComplete(true, "")
		}
	}()
}

func runSyncToCloud(ctx context.Context) error {
	wrap := func(err error) error { return fmt.Errorf("syncToCloud: %w", err) }

	token := _storage.GetCloudSessionToken()
	if token == "" {
		return wrap(fmt.Errorf("not connected"))
	}

	// Accounts blob.
	_storage.Lock()
	accounts := make([]Account, len(_storage.KnownAccounts))
	copy(accounts, _storage.KnownAccounts)
	_storage.Unlock()
	if err := putBlob(ctx, token, "accounts", accounts); err != nil {
		return wrap(err)
	}

	// Settings blob.
	_storage.Lock()
	syncSettings := cloudSyncableSettings{
		AutoRefreshMennoPref:       _storage.AutoRefreshMennoPref,
		RetryFailedMissions:        _storage.RetryFailedMissions,
		HideTimeoutErrors:          _storage.HideTimeoutErrors,
		WorkerCount:                _storage.WorkerCount,
		ScreenshotSafety:           _storage.ScreenshotSafety,
		ShowMissionProgress:        _storage.ShowMissionProgress,
		CollapseOlderSections:      _storage.CollapseOlderSections,
		AdvancedDropFilter:         _storage.AdvancedDropFilter,
		MissionViewByDate:          _storage.MissionViewByDate,
		MissionViewTimes:           _storage.MissionViewTimes,
		MissionRecolorDC:           _storage.MissionRecolorDC,
		MissionRecolorBC:           _storage.MissionRecolorBC,
		MissionShowExpectedDrops:   _storage.MissionShowExpectedDrops,
		MissionMultiViewMode:       _storage.MissionMultiViewMode,
		MissionSortMethod:          _storage.MissionSortMethod,
		LifetimeSortMethod:         _storage.LifetimeSortMethod,
		LifetimeShowDropsPerShip:   _storage.LifetimeShowDropsPerShip,
		LifetimeShowExpectedTotals: _storage.LifetimeShowExpectedTotals,
	}
	_storage.Unlock()
	if err := putBlob(ctx, token, "settings", syncSettings); err != nil {
		return wrap(err)
	}

	// Reports blob: all reports and groups for every known account.
	reportsBlob := cloudReportsBlob{}
	for _, acct := range accounts {
		rows, err := reportdb.RetrieveAccountReports(ctx, acct.Id)
		if err != nil {
			log.Warnf("cloud sync: reports for %s: %v", acct.Id, err)
			continue
		}
		for _, row := range rows {
			def, err := rowToReportDef(row)
			if err != nil {
				log.Warnf("cloud sync: rowToReportDef: %v", err)
				continue
			}
			reportsBlob.Reports = append(reportsBlob.Reports, def)
		}
		groups, err := reportdb.RetrieveAccountGroups(ctx, acct.Id)
		if err != nil {
			log.Warnf("cloud sync: groups for %s: %v", acct.Id, err)
			continue
		}
		reportsBlob.Groups = append(reportsBlob.Groups, groups...)
	}
	if err := putBlob(ctx, token, "reports", reportsBlob); err != nil {
		return wrap(err)
	}

	return nil
}

// handleRestoreFromCloud downloads all blobs and applies them locally. Fires
// _onCloudRestoreComplete when done.
func handleRestoreFromCloud() {
	go func() {
		if err := runRestoreFromCloud(context.Background()); err != nil {
			log.Errorf("cloud sync: restore failed: %v", err)
			if _onCloudRestoreComplete != nil {
				_onCloudRestoreComplete(false, err.Error())
			}
			return
		}
		_storage.SetCloudLastPullAt(time.Now())
		if _onCloudRestoreComplete != nil {
			_onCloudRestoreComplete(true, "")
		}
	}()
}

func runRestoreFromCloud(ctx context.Context) error {
	wrap := func(err error) error { return fmt.Errorf("restoreFromCloud: %w", err) }

	token := _storage.GetCloudSessionToken()
	if token == "" {
		return wrap(fmt.Errorf("not connected"))
	}

	// Restore accounts first (reports reference account IDs).
	var remoteAccounts []Account
	if err := getBlob(ctx, token, "accounts", &remoteAccounts); err != nil {
		return wrap(fmt.Errorf("accounts blob: %w", err))
	}
	for _, acct := range remoteAccounts {
		_storage.AddKnownAccount(acct)
	}
	_storage.Lock()
	_updateKnownAccounts(_storage.KnownAccounts)
	_storage.Unlock()

	// Restore settings.
	var remoteSets cloudSyncableSettings
	if err := getBlob(ctx, token, "settings", &remoteSets); err != nil {
		log.Warnf("cloud sync: settings blob: %v (skipping)", err)
	} else {
		applyCloudSettings(remoteSets)
	}

	// Restore reports (insert missing by ID).
	var remoteReports cloudReportsBlob
	if err := getBlob(ctx, token, "reports", &remoteReports); err != nil {
		log.Warnf("cloud sync: reports blob: %v (skipping)", err)
	} else {
		importRemoteReports(ctx, remoteReports)
	}

	return nil
}

// applyCloudSettings writes remote settings into _storage, skipping
// machine-specific fields like resolution and browser path.
func applyCloudSettings(s cloudSyncableSettings) {
	_storage.SetAutoRefreshMennoPref(s.AutoRefreshMennoPref)
	_storage.SetRetryFailedMissions(s.RetryFailedMissions)
	_storage.SetHideTimeoutErrors(s.HideTimeoutErrors)
	_storage.SetWorkerCount(s.WorkerCount)
	_storage.SetScreenshotSafety(s.ScreenshotSafety)
	_storage.SetShowMissionProgress(s.ShowMissionProgress)
	_storage.SetCollapseOlderSections(s.CollapseOlderSections)
	_storage.SetAdvancedDropFilter(s.AdvancedDropFilter)
	_storage.SetMissionViewByDate(s.MissionViewByDate)
	_storage.SetMissionViewTimes(s.MissionViewTimes)
	_storage.SetMissionRecolorDC(s.MissionRecolorDC)
	_storage.SetMissionRecolorBC(s.MissionRecolorBC)
	_storage.SetMissionShowExpectedDrops(s.MissionShowExpectedDrops)
	_storage.SetMissionMultiViewMode(s.MissionMultiViewMode)
	_storage.SetMissionSortMethod(s.MissionSortMethod)
	_storage.SetLifetimeSortMethod(s.LifetimeSortMethod)
	_storage.SetLifetimeShowDropsPerShip(s.LifetimeShowDropsPerShip)
	_storage.SetLifetimeShowExpectedTotals(s.LifetimeShowExpectedTotals)
}

// importRemoteReports inserts groups then reports that don't already exist locally.
func importRemoteReports(ctx context.Context, blob cloudReportsBlob) {
	seen := map[string]struct{}{}
	for _, g := range blob.Groups {
		if g.Id == "" {
			continue
		}
		if _, dup := seen[g.Id]; dup {
			continue
		}
		seen[g.Id] = struct{}{}
		if _, err := reportdb.InsertReportGroup(ctx, g); err != nil {
			log.Debugf("cloud sync: insert group %s: %v", g.Id, err)
		}
	}
	seenR := map[string]struct{}{}
	for _, def := range blob.Reports {
		if def.Id == "" {
			continue
		}
		if _, dup := seenR[def.Id]; dup {
			continue
		}
		seenR[def.Id] = struct{}{}
		row, err := reportDefToRow(def)
		if err != nil {
			log.Warnf("cloud sync: report def → row %s: %v", def.Id, err)
			continue
		}
		if err := reportdb.InsertReport(ctx, row); err != nil {
			log.Debugf("cloud sync: insert report %s: %v", def.Id, err)
		}
	}
}

// putBlob marshals payload and PUTs it to /blobs/<name>.
// encryptBlob encrypts plaintext with AES-256-GCM using hexKey (64-char hex = 32 bytes).
// The 12-byte random nonce is prepended to the ciphertext and the whole thing is base64-encoded.
func encryptBlob(hexKey string, plaintext []byte) (string, error) {
	key, err := hex.DecodeString(hexKey)
	if err != nil {
		return "", fmt.Errorf("encryptBlob: decode key: %w", err)
	}
	block, err := aes.NewCipher(key)
	if err != nil {
		return "", fmt.Errorf("encryptBlob: new cipher: %w", err)
	}
	gcm, err := cipher.NewGCM(block)
	if err != nil {
		return "", fmt.Errorf("encryptBlob: new gcm: %w", err)
	}
	nonce := make([]byte, gcm.NonceSize())
	if _, err := io.ReadFull(rand.Reader, nonce); err != nil {
		return "", fmt.Errorf("encryptBlob: nonce: %w", err)
	}
	sealed := gcm.Seal(nonce, nonce, plaintext, nil)
	return base64.StdEncoding.EncodeToString(sealed), nil
}

// decryptBlob reverses encryptBlob: base64-decodes, splits nonce, then AES-256-GCM decrypts.
func decryptBlob(hexKey, ciphertext string) ([]byte, error) {
	key, err := hex.DecodeString(hexKey)
	if err != nil {
		return nil, fmt.Errorf("decryptBlob: decode key: %w", err)
	}
	data, err := base64.StdEncoding.DecodeString(ciphertext)
	if err != nil {
		return nil, fmt.Errorf("decryptBlob: decode base64: %w", err)
	}
	block, err := aes.NewCipher(key)
	if err != nil {
		return nil, fmt.Errorf("decryptBlob: new cipher: %w", err)
	}
	gcm, err := cipher.NewGCM(block)
	if err != nil {
		return nil, fmt.Errorf("decryptBlob: new gcm: %w", err)
	}
	nonceSize := gcm.NonceSize()
	if len(data) < nonceSize {
		return nil, fmt.Errorf("decryptBlob: ciphertext too short")
	}
	plaintext, err := gcm.Open(nil, data[:nonceSize], data[nonceSize:], nil)
	if err != nil {
		return nil, fmt.Errorf("decryptBlob: decrypt: %w", err)
	}
	return plaintext, nil
}

func putBlob(ctx context.Context, token, name string, payload interface{}) error {
	raw, err := json.Marshal(payload)
	if err != nil {
		return fmt.Errorf("putBlob %s: marshal: %w", name, err)
	}
	encrypted, err := encryptBlob(_storage.GetCloudEncryptionKey(), raw)
	if err != nil {
		return fmt.Errorf("putBlob %s: encrypt: %w", name, err)
	}
	body, err := json.Marshal(blobEnvelope{Ciphertext: encrypted})
	if err != nil {
		return fmt.Errorf("putBlob %s: envelope: %w", name, err)
	}
	req, err := http.NewRequestWithContext(ctx, http.MethodPut,
		_cloudSyncBaseURL+"/blobs/"+name, bytes.NewReader(body))
	if err != nil {
		return fmt.Errorf("putBlob %s: request: %w", name, err)
	}
	req.Header.Set("Content-Type", "application/json")
	req.Header.Set("Authorization", "Bearer "+token)
	client := &http.Client{Timeout: _cloudSyncHTTPTimeout}
	resp, err := client.Do(req)
	if err != nil {
		return fmt.Errorf("putBlob %s: %w", name, err)
	}
	defer resp.Body.Close()
	if resp.StatusCode == http.StatusUnauthorized {
		return fmt.Errorf("putBlob %s: session expired - please reconnect", name)
	}
	if resp.StatusCode >= 400 {
		return fmt.Errorf("putBlob %s: server error %d", name, resp.StatusCode)
	}
	return nil
}

// getBlob fetches /blobs/<name> and unmarshals the data field into out.
func getBlob(ctx context.Context, token, name string, out interface{}) error {
	req, err := http.NewRequestWithContext(ctx, http.MethodGet,
		_cloudSyncBaseURL+"/blobs/"+name, nil)
	if err != nil {
		return fmt.Errorf("getBlob %s: request: %w", name, err)
	}
	req.Header.Set("Authorization", "Bearer "+token)
	client := &http.Client{Timeout: _cloudSyncHTTPTimeout}
	resp, err := client.Do(req)
	if err != nil {
		return fmt.Errorf("getBlob %s: %w", name, err)
	}
	defer resp.Body.Close()
	switch resp.StatusCode {
	case http.StatusUnauthorized:
		return fmt.Errorf("getBlob %s: session expired - please reconnect", name)
	case http.StatusNotFound:
		return fmt.Errorf("getBlob %s: not found (nothing synced yet?)", name)
	}
	if resp.StatusCode >= 400 {
		return fmt.Errorf("getBlob %s: server error %d", name, resp.StatusCode)
	}
	var env blobEnvelope
	if err := json.NewDecoder(resp.Body).Decode(&env); err != nil {
		return fmt.Errorf("getBlob %s: decode: %w", name, err)
	}
	plaintext, err := decryptBlob(_storage.GetCloudEncryptionKey(), env.Ciphertext)
	if err != nil {
		return fmt.Errorf("getBlob %s: decrypt: %w", name, err)
	}
	if err := json.Unmarshal(plaintext, out); err != nil {
		return fmt.Errorf("getBlob %s: unmarshal: %w", name, err)
	}
	return nil
}
