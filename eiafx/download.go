package eiafx

import (
	"encoding/json"
	"io"
	"net/http"
	"os"
	"path/filepath"
	"time"

	"github.com/DavidArthurCole/EggLedger/ei"
	log "github.com/sirupsen/logrus"
	"google.golang.org/protobuf/encoding/protojson"
)

const _configDownloadURL = "https://raw.githubusercontent.com/DavidArthurCole/EggLedger/master/eiafx/eiafx-config-min.json"
const _dataDownloadURL = "https://raw.githubusercontent.com/DavidArthurCole/EggLedger/master/eiafx/eiafx-data-min.json"

// tryDownloadRaw fetches url, runs validate on the body, then writes to cacheFile.
// Called in a goroutine - never blocks startup. Failures are logged as warnings.
func tryDownloadRaw(url, cacheFile string, validate func([]byte) error) {
	client := &http.Client{Timeout: 15 * time.Second}
	resp, err := client.Get(url)
	if err != nil {
		log.Warnf("eiafx: download failed (%s): %v", url, err)
		return
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		log.Warnf("eiafx: download returned HTTP %d (%s)", resp.StatusCode, url)
		return
	}

	body, err := io.ReadAll(resp.Body)
	if err != nil {
		log.Warnf("eiafx: reading download body (%s): %v", url, err)
		return
	}

	if err := validate(body); err != nil {
		log.Warnf("eiafx: downloaded content is invalid (%s): %v", url, err)
		return
	}

	if err := os.MkdirAll(filepath.Dir(cacheFile), 0755); err != nil {
		log.Warnf("eiafx: creating cache dir: %v", err)
		return
	}
	if err := os.WriteFile(cacheFile, body, 0644); err != nil {
		log.Warnf("eiafx: writing cache file: %v", err)
		return
	}
	log.Infof("eiafx: cache refreshed at %s", cacheFile)
}

// tryDownloadFresh downloads a fresh eiafx-config-min.json and validates it as protojson.
func tryDownloadFresh(cacheFile string) {
	tryDownloadRaw(_configDownloadURL, cacheFile, func(body []byte) error {
		var c ei.ArtifactsConfigurationResponse
		return protojson.Unmarshal(body, &c)
	})
}

// tryDownloadDataFresh downloads a fresh eiafx-data-min.json and validates it as JSON.
func tryDownloadDataFresh(cacheFile string) {
	tryDownloadRaw(_dataDownloadURL, cacheFile, func(body []byte) error {
		var v json.RawMessage
		return json.Unmarshal(body, &v)
	})
}
