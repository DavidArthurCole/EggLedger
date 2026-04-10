package main

import (
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"os"
	"path/filepath"
	"time"
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

const _mennoDateFormat = "2006-01-02-15-04-05"
const _mennoFileFormat = "menno-data-%s.json"

func loadLatestMennoData() (data MennoData, err error) {
	_storage.Lock()
	latestRefresh := _storage.LastMennoDataRefreshAt
	_storage.Unlock()

	//If the latest refresh is zero, return an empty data set, with an error.
	if latestRefresh.IsZero() {
		return MennoData{}, fmt.Errorf("no menno data available")
	}

	// Get the file name for the latest data.
	filename := fmt.Sprintf(_mennoFileFormat, latestRefresh.Format(_mennoDateFormat))
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
	if !_storage.AutoRefreshMennoPref {
		return false
	}

	// Update every 5 days.
	_storage.Lock()
	lastMennoRefesh := _storage.LastMennoDataRefreshAt
	_storage.Unlock()

	return (time.Since(lastMennoRefesh) > time.Hour*24*5)
}

func refreshMennoData(onProgress func(MennoDownloadProgress)) (err error) {

	// Fetch the data from the Menno server.
	resp, err := http.Get("https://eggincdatacollection.azurewebsites.net/api/GetAllData")
	if err != nil {
		_storage.SetLastMennoDataRefreshAt(time.Time{}) // Reset time to allow for a new refresh.
		fmt.Println(err)
		return err
	}
	defer resp.Body.Close()

	// Read response in chunks, emitting progress after each chunk.
	startTime := time.Now()
	totalBytes := resp.ContentLength // -1 if unknown
	var buf []byte
	chunk := make([]byte, 32*1024)
	for {
		n, readErr := resp.Body.Read(chunk)
		if n > 0 {
			buf = append(buf, chunk[:n]...)
			elapsed := time.Since(startTime).Seconds()
			var speedBps float64
			if elapsed > 0 {
				speedBps = float64(len(buf)) / elapsed
			}
			etaSeconds := -1.0
			if totalBytes > 0 && speedBps > 0 {
				etaSeconds = float64(totalBytes-int64(len(buf))) / speedBps
			}
			onProgress(MennoDownloadProgress{
				BytesRead:  int64(len(buf)),
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
	body := buf

	// Unmarshal
	var result interface{}
	err = json.Unmarshal(body, &result)
	if err != nil {
		_storage.SetLastMennoDataRefreshAt(time.Time{}) // Reset time to allow for a new refresh.
		fmt.Println(err)
		return err
	}

	_storage.Lock()
	oldDataTime := _storage.LastMennoDataRefreshAt
	_storage.Unlock()

	// Save the JSON to a file with a date-time stamp.
	newTime := time.Now()
	filename := fmt.Sprintf(_mennoFileFormat, newTime.Format(_mennoDateFormat))
	filePath := filepath.Join(_internalDir, filename)

	err = os.WriteFile(filePath, body, 0644)
	if err != nil {
		_storage.SetLastMennoDataRefreshAt(time.Time{}) // Reset time to allow for a new refresh.
		fmt.Println(err)
		return err
	}

	// Update the last refresh time.
	_storage.SetLastMennoDataRefreshAt(newTime)

	//Remove old file - as long as it's not the default time (0001-01-01-00-00-00)
	if !oldDataTime.IsZero() {
		oldFileName := fmt.Sprintf(_mennoFileFormat, oldDataTime.Format(_mennoDateFormat))
		oldFilePath := filepath.Join(_internalDir, oldFileName)
		err = os.Remove(oldFilePath)
		if err != nil {
			fmt.Println(err)
		}
	}

	// Return nil if everything went well.
	return nil
}
