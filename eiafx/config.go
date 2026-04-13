package eiafx

import (
	_ "embed"
	"os"
	"path/filepath"
	"time"

	"github.com/DavidArthurCole/EggLedger/ei"
	"github.com/pkg/errors"
	log "github.com/sirupsen/logrus"
	"google.golang.org/protobuf/encoding/protojson"
)

//go:embed eiafx-config-min.json
var _eiafxConfigJSON []byte

// Config holds the active eiafx configuration. Populated by LoadConfig.
var Config *ei.ArtifactsConfigurationResponse

const _cacheFilename = "eiafx-config.json"
const _cacheTTL = 7 * 24 * time.Hour

// LoadConfig loads eiafx configuration into Config. Priority:
//  1. internal/eiafx-config.json if present and <7 days old
//  2. Embedded eiafx-config-min.json (fallback, always succeeds)
//
// If the cached file is stale or absent, a background goroutine attempts to
// download a fresh copy for the next cold start. Startup is never blocked.
func LoadConfig(internalDir string) error {
	wrap := func(err error) error { return errors.Wrap(err, "eiafx.LoadConfig") }

	cacheFile := filepath.Join(internalDir, _cacheFilename)
	if data, ok := readIfFresh(cacheFile); ok {
		var c ei.ArtifactsConfigurationResponse
		if err := protojson.Unmarshal(data, &c); err == nil {
			Config = &c
			return nil
		}
		// Corrupted cache - fall through to embedded and schedule repair.
		log.Warnf("eiafx: cached file is corrupt, will re-download: %v", cacheFile)
		go tryDownloadFresh(cacheFile)
	}

	// Use embedded fallback immediately.
	var c ei.ArtifactsConfigurationResponse
	if err := protojson.Unmarshal(_eiafxConfigJSON, &c); err != nil {
		return wrap(err)
	}
	Config = &c

	// Background refresh for next cold start.
	go tryDownloadFresh(cacheFile)

	return nil
}

// readIfFresh returns the file contents if the file exists and was modified within the last 7 days.
func readIfFresh(path string) ([]byte, bool) {
	info, err := os.Stat(path)
	if err != nil {
		return nil, false
	}
	if time.Since(info.ModTime()) > _cacheTTL {
		return nil, false
	}
	data, err := os.ReadFile(path)
	if err != nil {
		return nil, false
	}
	return data, true
}
