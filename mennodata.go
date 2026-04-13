package main

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
	"time"

	"github.com/pkg/errors"
	log "github.com/sirupsen/logrus"
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
	_storage.Lock()
	latestRefresh := _storage.LastMennoDataRefreshAt
	_storage.Unlock()

	//If the latest refresh is zero, return an empty data set, with an error.
	if latestRefresh.IsZero() {
		return MennoData{}, fmt.Errorf("no menno data available")
	}

	// Get the file name for the latest data.
	filename := "menno-data.json"
	filePath := filepath.Join(_internalDir, filename)

	returnData := []ConfigurationItem{}

	// Read the file from disk.
	file, err := os.ReadFile(filePath)
	if err != nil {
		_storage.SetLastMennoDataRefreshAt(time.Time{}) // Reset time to allow for a new refresh.
		fmt.Println(err)
		return MennoData{
			ConfigurationItems: returnData,
		}, err
	}

	// Unmarshal the JSON data.
	err = json.Unmarshal(file, &returnData)
	if err != nil {
		_storage.SetLastMennoDataRefreshAt(time.Time{}) // Reset time to allow for a new refresh.
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
	if _forceMennoRefresh {
		return true
	}
	_storage.Lock()
	autoPref := _storage.AutoRefreshMennoPref
	lastMennoRefresh := _storage.LastMennoDataRefreshAt
	_storage.Unlock()
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

	// Fetch the gzipped data from the Menno server.
	resp, err := http.Get("https://eggincdatacollectionsa.blob.core.windows.net/mission-data/all-data.json.gz")
	if err != nil {
		_storage.SetLastMennoDataRefreshAt(time.Time{}) // Reset time to allow for a new refresh.
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
			_storage.SetLastMennoDataRefreshAt(time.Time{})
			fmt.Println(readErr)
			return readErr
		}
	}

	// Signal that decompression is starting.
	onProgress(MennoDownloadProgress{Phase: "unzipping"})

	// Decompress the gzipped body.
	gr, err := gzip.NewReader(bytes.NewReader(compressedBuf))
	if err != nil {
		_storage.SetLastMennoDataRefreshAt(time.Time{})
		fmt.Println(err)
		return err
	}
	defer gr.Close()
	body, err := io.ReadAll(gr)
	if err != nil {
		_storage.SetLastMennoDataRefreshAt(time.Time{})
		fmt.Println(err)
		return err
	}

	// Signal that disk IO is starting.
	onProgress(MennoDownloadProgress{Phase: "saving"})

	// Decode into typed struct to catch schema drift early.
	var data MennoData
	if err := json.Unmarshal(body, &data); err != nil {
		_storage.SetLastMennoDataRefreshAt(time.Time{})
		return wrap(errors.Wrap(err, "failed to decode Menno data response"))
	}
	if len(data.ConfigurationItems) == 0 {
		_storage.SetLastMennoDataRefreshAt(time.Time{})
		return wrap(errors.New("Menno data response was empty or schema has changed"))
	}
	encoded, err := json.Marshal(data.ConfigurationItems)
	if err != nil {
		_storage.SetLastMennoDataRefreshAt(time.Time{})
		return wrap(errors.Wrap(err, "failed to re-encode Menno data"))
	}

	_storage.Lock()
	oldDataTime := _storage.LastMennoDataRefreshAt
	_storage.Unlock()

	// Save the JSON to a temp new file.
	newTime := time.Now()
	filename := "menno-data-new.json"
	filePath := filepath.Join(_internalDir, filename)

	err = os.WriteFile(filePath, encoded, 0644)
	if err != nil {
		_storage.SetLastMennoDataRefreshAt(time.Time{}) // Reset time to allow for a new refresh.
		fmt.Println(err)
		return err
	}

	// Update the last refresh time.
	_storage.SetLastMennoDataRefreshAt(newTime)

	//Remove old file - as long as it's not the default time (0001-01-01-00-00-00)
	if !oldDataTime.IsZero() {
		oldFileName := "menno-data.json"
		oldFilePath := filepath.Join(_internalDir, oldFileName)
		err = os.Remove(oldFilePath)
		if err != nil {
			fmt.Println(err)
		}
	}

	// Rename new file to the standard name.
	err = os.Rename(filePath, filepath.Join(_internalDir, "menno-data.json"))
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

func handleIsMennoRefreshNeeded() bool {
	return checkIfRefreshMennoDataIsNeeded()
}

func handleSecondsSinceLastMennoUpdate() int {
	_storage.Lock()
	lastRefresh := _storage.LastMennoDataRefreshAt
	_storage.Unlock()
	if lastRefresh.IsZero() {
		return math.MaxInt32
	}
	return int(time.Since(lastRefresh).Seconds())
}

func handleLoadMennoData() bool {
	var err error
	_latestMennoData, err = loadLatestMennoData()
	if err != nil {
		if !strings.Contains(err.Error(), "no menno data available") {
			log.Error(err)
		}
		return false
	}
	return true
}

func handleGetMennoData(ship, shipDuration, shipLevel, targetArtifact int) []ConfigurationItem {
	if len(_latestMennoData.ConfigurationItems) == 0 {
		var err error
		_latestMennoData, err = loadLatestMennoData()
		if err != nil || len(_latestMennoData.ConfigurationItems) == 0 {
			log.Error(err)
			return nil
		}
	}
	filteredMennoData := MennoData{}
	for _, configurationItem := range _latestMennoData.ConfigurationItems {
		sc := configurationItem.ShipConfiguration
		if sc.ShipType.Id == ship && sc.ShipDurationType.Id == shipDuration &&
			sc.Level == shipLevel && sc.TargetArtifact.Id == targetArtifact {
			filteredMennoData.ConfigurationItems = append(
				filteredMennoData.ConfigurationItems, configurationItem,
			)
		}
	}
	return filteredMennoData.ConfigurationItems
}

func handleUpdateMennoData(onDone func(bool), onProgress func(MennoDownloadProgress)) {
	err := refreshMennoData(onProgress)
	onDone(err == nil)
}
