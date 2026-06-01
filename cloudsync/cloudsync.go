package cloudsync

// cloudsync.go - Discord OAuth2 authentication and blob sync against
// https://ledgersync.davidarthurcole.me
//
// Blobs are AES-256-GCM encrypted client-side before upload. The encryption key
// is generated server-side on first auth and returned with every subsequent auth,
// so the same key is available on all devices after reconnecting.

import (
	"net/http"
	"time"

	"github.com/DavidArthurCole/EggLedger/reportdb"
	"github.com/DavidArthurCole/EggLedger/reports"
	"github.com/DavidArthurCole/EggLedger/storage"
	log "github.com/sirupsen/logrus"
)

const (
	_cloudSyncBaseURL      = "https://ledgersync.davidarthurcole.me/api/v1"
	_cloudSyncHTTPTimeout  = 10 * time.Second
	_cloudAuthPollTimeout  = 5 * time.Minute
	_cloudAuthPollInterval = 2 * time.Second
	_cloudReachableTimeout = 5 * time.Second
)

// Shared HTTP clients (reused across requests so connections/transports are
// pooled rather than re-allocated per call).
var (
	_cloudClient     = &http.Client{Timeout: _cloudSyncHTTPTimeout}
	_reachableClient = &http.Client{Timeout: _cloudReachableTimeout}
)

// Injected dependencies, wired by main() via SetStorage / SetCallbacks / SetReportConverters.
var (
	_storage *storage.AppStorage

	onCloudSyncComplete    func(success bool, errMsg string)
	onCloudRestoreComplete func(success bool, errMsg string)
	onDiscordAuthComplete  func(connected bool, username string)
	onKnownAccountsUpdate  func(accounts []storage.Account)

	reportDefToRow func(def reports.ReportDefinition) (reportdb.ReportRow, error)
	rowToReportDef func(r reportdb.ReportRow) (reports.ReportDefinition, error)
)

// SetStorage injects the shared AppStorage pointer owned by main.
func SetStorage(s *storage.AppStorage) { _storage = s }

// SetCallbacks injects the Go-to-JS callbacks owned by main.
func SetCallbacks(
	syncComplete func(bool, string),
	restoreComplete func(bool, string),
	discordAuth func(bool, string),
	knownAccounts func([]storage.Account),
) {
	onCloudSyncComplete = syncComplete
	onCloudRestoreComplete = restoreComplete
	onDiscordAuthComplete = discordAuth
	onKnownAccountsUpdate = knownAccounts
}

// SetReportConverters injects the report row <-> definition converters that live in main.
func SetReportConverters(
	defToRow func(reports.ReportDefinition) (reportdb.ReportRow, error),
	rowToDef func(reportdb.ReportRow) (reports.ReportDefinition, error),
) {
	reportDefToRow = defToRow
	rowToReportDef = rowToDef
}

// CloudSyncStatus is returned to the UI by GetStatus.
type CloudSyncStatus struct {
	Connected        bool   `json:"connected"`
	Username         string `json:"username"`
	AvatarURL        string `json:"avatarUrl"`
	LastPushAt       int64  `json:"lastPushAt"`
	LastPullAt       int64  `json:"lastPullAt"`
	HasEncryptionKey bool   `json:"hasEncryptionKey"`
}

// checkCloudSyncReachable probes /verify with a short timeout.
func checkCloudSyncReachable() bool {
	client := _reachableClient
	resp, err := client.Get(_cloudSyncBaseURL + "/verify")
	if err != nil {
		log.Debugf("cloud sync: reachability probe failed: %v", err)
		return false
	}
	resp.Body.Close()
	return resp.StatusCode == http.StatusOK
}

// GetStatus returns the current cloud sync state.
func GetStatus() CloudSyncStatus {
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

// CheckReachable performs the reachability probe synchronously.
func CheckReachable() bool {
	return checkCloudSyncReachable()
}
