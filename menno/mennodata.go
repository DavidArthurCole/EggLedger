package menno

import (
	"bytes"
	"compress/gzip"
	"encoding/json"
	"fmt"
	"io"
	"math"
	"net/http"
	"os"
	"path/filepath"
	"strings"
	"sync"
	"time"

	"github.com/pkg/errors"
	log "github.com/sirupsen/logrus"
)

// _latestMennoData caches the most recently loaded Menno data set. It is read
// and written from lorca binding goroutines (LoadData/GetData), so all access
// goes through _mennoMu. The cached slice is only ever replaced wholesale, so a
// reader may snapshot the slice header under RLock and iterate it after
// unlocking.
var (
	_latestMennoData = MennoData{}
	_mennoMu         sync.RWMutex
)

type MennoData struct {
	ConfigurationItems []ConfigurationItem `json:"configurationItems"`
}

type ConfigurationItem struct {
	ShipConfiguration     ShipConfiguration     `json:"shipConfiguration"`
	ArtifactConfiguration ArtifactConfiguration `json:"artifactConfiguration"`
	TotalDrops            int                   `json:"totalDrops"`
}

type ShipConfiguration struct {
	ShipType         IdNamePair `json:"shipType"`
	ShipDurationType IdNamePair `json:"shipDurationType"`
	Level            int        `json:"level"`
	TargetArtifact   IdNamePair `json:"targetArtifact"`
}

type ArtifactConfiguration struct {
	ArtifactType   IdNamePair `json:"artifactType"`
	ArtifactRarity IdNamePair `json:"artifactRarity"`
	ArtifactLevel  int        `json:"artifactLevel"`
}

type IdNamePair struct {
	Id   int    `json:"id"`
	Name string `json:"name"`
}

func loadLatestMennoData() (data MennoData, err error) {
	latestRefresh := _host.LastRefreshAt()

	//If the latest refresh is zero, return an empty data set, with an error.
	if latestRefresh.IsZero() {
		return MennoData{}, fmt.Errorf("no menno data available")
	}

	// Get the file name for the latest data.
	filename := "menno-data.json"
	filePath := filepath.Join(_host.InternalDir(), filename)

	returnData := []ConfigurationItem{}

	// Read the file from disk.
	file, err := os.ReadFile(filePath)
	if err != nil {
		_host.SetLastRefreshAt(time.Time{}) // Reset time to allow for a new refresh.
		fmt.Println(err)
		return MennoData{
			ConfigurationItems: returnData,
		}, err
	}

	// Unmarshal the JSON data.
	err = json.Unmarshal(file, &returnData)
	if err != nil {
		_host.SetLastRefreshAt(time.Time{}) // Reset time to allow for a new refresh.
		fmt.Println(err)
		return MennoData{
			ConfigurationItems: returnData,
		}, err
	}

	return MennoData{
		ConfigurationItems: returnData,
	}, nil
}

func checkIfRefreshMennoDataIsNeeded() bool {
	if _host.ForceRefresh() {
		return true
	}
	autoPref := _host.AutoRefreshPref()
	lastMennoRefresh := _host.LastRefreshAt()
	if !autoPref {
		return false
	}
	return time.Since(lastMennoRefresh) > time.Hour*24*5
}

func refreshMennoData(onProgress func(MennoDownloadProgress)) (err error) {
	action := "refresh Menno data"
	wrap := func(e error) error { return errors.Wrap(e, action) }

	// Signal that we are connecting before the request goes out.
	onProgress(MennoDownloadProgress{Phase: "connecting"})

	// Fetch the gzipped data from the Menno server. Use an explicit client
	// timeout so a stalled endpoint cannot hang this download goroutine forever.
	client := &http.Client{Timeout: 60 * time.Second}
	resp, err := client.Get("https://eggincdatacollectionsa.blob.core.windows.net/mission-data/all-data.json.gz")
	if err != nil {
		_host.SetLastRefreshAt(time.Time{}) // Reset time to allow for a new refresh.
		fmt.Println(err)
		return err
	}
	defer resp.Body.Close()

	// Read compressed response in chunks, emitting progress after each chunk.
	startTime := time.Now()
	totalBytes := resp.ContentLength // compressed size, -1 if unknown
	var compressedBuf []byte
	chunk := make([]byte, 32*1024)
	for {
		n, readErr := resp.Body.Read(chunk)
		if n > 0 {
			compressedBuf = append(compressedBuf, chunk[:n]...)
			elapsed := time.Since(startTime).Seconds()
			var speedBps float64
			if elapsed > 0 {
				speedBps = float64(len(compressedBuf)) / elapsed
			}
			etaSeconds := -1.0
			if totalBytes > 0 && speedBps > 0 {
				etaSeconds = float64(totalBytes-int64(len(compressedBuf))) / speedBps
			}
			onProgress(MennoDownloadProgress{
				Phase:      "downloading",
				BytesRead:  int64(len(compressedBuf)),
				TotalBytes: totalBytes,
				SpeedBps:   speedBps,
				ETASeconds: etaSeconds,
			})
		}
		if readErr == io.EOF {
			break
		}
		if readErr != nil {
			_host.SetLastRefreshAt(time.Time{})
			fmt.Println(readErr)
			return readErr
		}
	}

	// Signal that decompression is starting.
	onProgress(MennoDownloadProgress{Phase: "unzipping"})

	// Decompress the gzipped body.
	gr, err := gzip.NewReader(bytes.NewReader(compressedBuf))
	if err != nil {
		_host.SetLastRefreshAt(time.Time{})
		fmt.Println(err)
		return err
	}
	defer gr.Close()
	body, err := io.ReadAll(gr)
	if err != nil {
		_host.SetLastRefreshAt(time.Time{})
		fmt.Println(err)
		return err
	}

	// Signal that disk IO is starting.
	onProgress(MennoDownloadProgress{Phase: "saving"})

	// Decode into typed slice to catch schema drift early.
	// The Menno endpoint returns a raw JSON array of ConfigurationItem.
	var items []ConfigurationItem
	if err := json.Unmarshal(body, &items); err != nil {
		_host.SetLastRefreshAt(time.Time{})
		return wrap(errors.Wrap(err, "failed to decode Menno data response"))
	}
	if len(items) == 0 {
		_host.SetLastRefreshAt(time.Time{})
		return wrap(errors.New("Menno data response was empty or schema has changed"))
	}
	encoded, err := json.Marshal(items)
	if err != nil {
		_host.SetLastRefreshAt(time.Time{})
		return wrap(errors.Wrap(err, "failed to re-encode Menno data"))
	}

	oldDataTime := _host.LastRefreshAt()

	// Save the JSON to a temp new file.
	newTime := time.Now()
	filename := "menno-data-new.json"
	filePath := filepath.Join(_host.InternalDir(), filename)

	err = os.WriteFile(filePath, encoded, 0644)
	if err != nil {
		_host.SetLastRefreshAt(time.Time{}) // Reset time to allow for a new refresh.
		fmt.Println(err)
		return err
	}

	// Update the last refresh time.
	_host.SetLastRefreshAt(newTime)

	//Remove old file - as long as it's not the default time (0001-01-01-00-00-00)
	if !oldDataTime.IsZero() {
		oldFileName := "menno-data.json"
		oldFilePath := filepath.Join(_host.InternalDir(), oldFileName)
		err = os.Remove(oldFilePath)
		if err != nil {
			fmt.Println(err)
		}
	}

	// Rename new file to the standard name.
	err = os.Rename(filePath, filepath.Join(_host.InternalDir(), "menno-data.json"))
	if err != nil {
		fmt.Println(err)
		return err
	}

	// Return nil if everything went well.
	return nil
}

type MennoDownloadProgress struct {
	// "connecting" | "downloading" | "saving"
	Phase      string  `json:"phase"`
	BytesRead  int64   `json:"bytesRead"`
	TotalBytes int64   `json:"totalBytes"`
	SpeedBps   float64 `json:"speedBps"`
	ETASeconds float64 `json:"etaSeconds"`
}

// IsRefreshNeeded reports whether the cached Menno data should be refreshed.
func IsRefreshNeeded() bool {
	return checkIfRefreshMennoDataIsNeeded()
}

// SecondsSinceLastUpdate returns the number of seconds since the last Menno
// data refresh, or math.MaxInt32 if there has never been a refresh.
func SecondsSinceLastUpdate() int {
	lastRefresh := _host.LastRefreshAt()
	if lastRefresh.IsZero() {
		return math.MaxInt32
	}
	return int(time.Since(lastRefresh).Seconds())
}

// LoadData loads the cached Menno data set into the package-level cache.
func LoadData() bool {
	data, err := loadLatestMennoData()
	if err != nil {
		if !strings.Contains(err.Error(), "no menno data available") {
			log.Error(err)
		}
		return false
	}
	_mennoMu.Lock()
	_latestMennoData = data
	_mennoMu.Unlock()
	return true
}

func filterMennoItems(ship, shipDuration, shipLevel, targetArtifact int) []ConfigurationItem {
	_mennoMu.RLock()
	items := _latestMennoData.ConfigurationItems
	_mennoMu.RUnlock()
	var result []ConfigurationItem
	for _, item := range items {
		sc := item.ShipConfiguration
		if sc.ShipType.Id == ship && sc.ShipDurationType.Id == shipDuration &&
			sc.Level == shipLevel && sc.TargetArtifact.Id == targetArtifact {
			result = append(result, item)
		}
	}
	return result
}

// mennoNoTarget is the sentinel target ID Menno uses when no target artifact is set.
const mennoNoTarget = 10000

// GetData returns the cached Menno configuration items matching the supplied
// ship configuration, falling back to the no-target set when appropriate.
func GetData(ship, shipDuration, shipLevel, targetArtifact int) []ConfigurationItem {
	_mennoMu.RLock()
	empty := len(_latestMennoData.ConfigurationItems) == 0
	_mennoMu.RUnlock()
	if empty {
		data, err := loadLatestMennoData()
		if err != nil || len(data.ConfigurationItems) == 0 {
			log.Error(err)
			return nil
		}
		_mennoMu.Lock()
		_latestMennoData = data
		_mennoMu.Unlock()
	}
	items := filterMennoItems(ship, shipDuration, shipLevel, targetArtifact)
	if len(items) == 0 && targetArtifact != mennoNoTarget {
		items = filterMennoItems(ship, shipDuration, shipLevel, mennoNoTarget)
	}
	return items
}

// UpdateData refreshes the Menno data set, invoking onProgress as the download
// proceeds and onDone with the success status when finished.
func UpdateData(onDone func(bool), onProgress func(MennoDownloadProgress)) {
	err := refreshMennoData(onProgress)
	onDone(err == nil)
}
