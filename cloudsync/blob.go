package cloudsync

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
	"github.com/DavidArthurCole/EggLedger/storage"
	log "github.com/sirupsen/logrus"
)

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

// SyncToCloud serializes and uploads all blobs. Fires onCloudSyncComplete when done.
func SyncToCloud() {
	go func() {
		if err := runSyncToCloud(context.Background()); err != nil {
			log.Errorf("cloud sync: sync failed: %v", err)
			if onCloudSyncComplete != nil {
				onCloudSyncComplete(false, err.Error())
			}
			return
		}
		_storage.SetCloudLastPushAt(time.Now())
		if onCloudSyncComplete != nil {
			onCloudSyncComplete(true, "")
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
	accounts := make([]storage.Account, len(_storage.KnownAccounts))
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

// RestoreFromCloud downloads all blobs and applies them locally. Fires
// onCloudRestoreComplete when done.
func RestoreFromCloud() {
	go func() {
		if err := runRestoreFromCloud(context.Background()); err != nil {
			log.Errorf("cloud sync: restore failed: %v", err)
			if onCloudRestoreComplete != nil {
				onCloudRestoreComplete(false, err.Error())
			}
			return
		}
		_storage.SetCloudLastPullAt(time.Now())
		if onCloudRestoreComplete != nil {
			onCloudRestoreComplete(true, "")
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
	var remoteAccounts []storage.Account
	if err := getBlob(ctx, token, "accounts", &remoteAccounts); err != nil {
		return wrap(fmt.Errorf("accounts blob: %w", err))
	}
	for _, acct := range remoteAccounts {
		_storage.AddKnownAccount(acct)
	}
	// Snapshot the accounts under lock, then invoke the callback unlocked: the
	// callback must not run while holding the storage mutex (it could re-enter a
	// storage getter and deadlock), and it must not retain a reference to the
	// live slice.
	_storage.Lock()
	accountsCopy := make([]storage.Account, len(_storage.KnownAccounts))
	copy(accountsCopy, _storage.KnownAccounts)
	_storage.Unlock()
	onKnownAccountsUpdate(accountsCopy)

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
			log.Warnf("cloud sync: report def -> row %s: %v", def.Id, err)
			continue
		}
		if err := reportdb.InsertReport(ctx, row); err != nil {
			log.Debugf("cloud sync: insert report %s: %v", def.Id, err)
		}
	}
}

// DeleteRemoteData overwrites all server-side blobs with empty payloads,
// effectively clearing all synced data while keeping the session active.
func DeleteRemoteData() error {
	ctx := context.Background()
	wrap := func(err error) error { return fmt.Errorf("deleteRemoteData: %w", err) }
	token := _storage.GetCloudSessionToken()
	if token == "" {
		return wrap(fmt.Errorf("not connected"))
	}
	if err := putBlob(ctx, token, "accounts", []storage.Account{}); err != nil {
		return wrap(err)
	}
	if err := putBlob(ctx, token, "settings", cloudSyncableSettings{}); err != nil {
		return wrap(err)
	}
	if err := putBlob(ctx, token, "reports", cloudReportsBlob{}); err != nil {
		return wrap(err)
	}
	return nil
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
	client := _cloudClient
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
	client := _cloudClient
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
