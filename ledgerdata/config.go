package ledgerdata

import (
	_ "embed"
	"encoding/json"
	"os"
	"path/filepath"
	"time"

	"github.com/pkg/errors"
	log "github.com/sirupsen/logrus"
)

//go:embed ledger-display-data-min.json
var _embeddedJSON []byte

// Config holds the active display data. Populated by LoadConfig.
var Config LedgerDisplayData

// LedgerDisplayData contains all game display strings extracted from source.
type LedgerDisplayData struct {
	// ArtifactEffects maps proto enum name (e.g. "LUNAR_TOTEM") to a
	// [level][rarity] string matrix of effect substitution values.
	ArtifactEffects          map[string][][]string `json:"artifactEffects"`
	FarmerRoles              []FarmerRole          `json:"farmerRoles"`
	ShipNames                map[string]string     `json:"shipNames"`
	ArtifactTargets          []ArtifactTarget      `json:"artifactTargets"`
	ArtifactTierNames        map[string][]string   `json:"artifactTierNames"`
	InventoryVisualizerOrder map[string]int        `json:"inventoryVisualizerOrder"`
	GenericBenefitStrings    map[string]string     `json:"genericBenefitStrings"`
	ArtifactTypes            map[string]string     `json:"artifactTypes"`
	StoneFragmentMap         map[string]string     `json:"stoneFragmentMap"`
}

// FarmerRole maps an EB order-of-magnitude bucket to a display tier.
type FarmerRole struct {
	OOM   int    `json:"oom"`
	Name  string `json:"name"`
	Color string `json:"color"`
}

// ArtifactTarget is a filterable mission target entry with display metadata.
type ArtifactTarget struct {
	Name        string `json:"name"`
	DisplayName string `json:"displayName"`
	ImageString string `json:"imageString"`
}

const _cacheFilename = "ledger-display-data.json"
const _cacheTTL = 7 * 24 * time.Hour

// LoadConfig loads display data into Config. Priority:
//  1. internal/ledger-display-data.json if present and <7 days old
//  2. Embedded ledger-display-data-min.json (fallback, always succeeds)
//
// If the cached file is stale or absent, a background goroutine attempts to
// download a fresh copy for the next cold start. Startup is never blocked.
func LoadConfig(internalDir string) error {
	wrap := func(err error) error { return errors.Wrap(err, "ledgerdata.LoadConfig") }

	cacheFile := filepath.Join(internalDir, _cacheFilename)
	if data, ok := readIfFresh(cacheFile); ok {
		var d LedgerDisplayData
		if err := json.Unmarshal(data, &d); err == nil {
			Config = d
			return nil
		}
		// Corrupted cache - fall through to embedded and schedule repair.
		log.Warnf("ledgerdata: cached file is corrupt, will re-download: %v", cacheFile)
		go tryDownloadFresh(cacheFile)
	}

	// Use embedded fallback immediately.
	var d LedgerDisplayData
	if err := json.Unmarshal(_embeddedJSON, &d); err != nil {
		return wrap(err)
	}
	Config = d

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
