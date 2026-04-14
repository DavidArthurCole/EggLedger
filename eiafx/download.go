package eiafx

import (
	"io"
	"net/http"
	"os"
	"path/filepath"
	"time"

	"github.com/DavidArthurCole/EggLedger/ei"
	log "github.com/sirupsen/logrus"
	"google.golang.org/protobuf/encoding/protojson"
)

const _downloadURL = "https://raw.githubusercontent.com/DavidArthurCole/EggLedger/master/eiafx/eiafx-config-min.json"

// tryDownloadFresh attempts to download a fresh copy of eiafx-config-min.json
// and write it to cacheFile. Called in a goroutine - never blocks startup.
// Failures are logged as warnings; the embedded fallback remains active.
func tryDownloadFresh(cacheFile string) {
	client := &http.Client{Timeout: 15 * time.Second}
	resp, err := client.Get(_downloadURL)
	if err != nil {
		log.Warnf("eiafx: download failed: %v", err)
		return
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		log.Warnf("eiafx: download returned HTTP %d", resp.StatusCode)
		return
	}

	body, err := io.ReadAll(resp.Body)
	if err != nil {
		log.Warnf("eiafx: reading download body: %v", err)
		return
	}

	// Validate the downloaded JSON before writing.
	var c ei.ArtifactsConfigurationResponse
	if err := protojson.Unmarshal(body, &c); err != nil {
		log.Warnf("eiafx: downloaded JSON is invalid: %v", err)
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
