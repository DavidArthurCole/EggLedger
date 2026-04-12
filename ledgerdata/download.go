package ledgerdata

import (
	"encoding/json"
	"io"
	"net/http"
	"os"
	"path/filepath"
	"time"

	log "github.com/sirupsen/logrus"
)

const _downloadURL = "https://raw.githubusercontent.com/DavidArthurCole/EggLedger/master/ledgerdata/ledger-display-data.json"

// tryDownloadFresh attempts to download a fresh copy of ledger-display-data.json
// and write it to cacheFile. Called in a goroutine - never blocks startup.
// Failures are logged as warnings; the embedded fallback remains active.
func tryDownloadFresh(cacheFile string) {
	client := &http.Client{Timeout: 15 * time.Second}
	resp, err := client.Get(_downloadURL)
	if err != nil {
		log.Warnf("ledgerdata: download failed: %v", err)
		return
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		log.Warnf("ledgerdata: download returned HTTP %d", resp.StatusCode)
		return
	}

	body, err := io.ReadAll(resp.Body)
	if err != nil {
		log.Warnf("ledgerdata: reading download body: %v", err)
		return
	}

	// Validate the downloaded JSON before writing.
	var d LedgerDisplayData
	if err := json.Unmarshal(body, &d); err != nil {
		log.Warnf("ledgerdata: downloaded JSON is invalid: %v", err)
		return
	}

	if err := os.MkdirAll(filepath.Dir(cacheFile), 0755); err != nil {
		log.Warnf("ledgerdata: creating cache dir: %v", err)
		return
	}
	if err := os.WriteFile(cacheFile, body, 0644); err != nil {
		log.Warnf("ledgerdata: writing cache file: %v", err)
		return
	}
	log.Infof("ledgerdata: cache refreshed at %s", cacheFile)
}
