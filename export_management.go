package main

import (
	"fmt"
	"os"
	"path/filepath"
	"regexp"
	"sort"
	"time"
)

// ExportFilePair represents one timestamp group (csv + xlsx) for one EID.
type ExportFilePair struct {
	Timestamp   string `json:"timestamp"`   // "20240311_221737"
	DisplayDate string `json:"displayDate"` // "2024-03-11 22:17"
	CsvPath     string `json:"csvPath"`     // absolute path, or "" if absent
	CsvSize     int64  `json:"csvSize"`
	XlsxPath    string `json:"xlsxPath"`
	XlsxSize    int64  `json:"xlsxSize"`
}

// ExportGroup holds all timestamp groups for one EID, newest-first.
type ExportGroup struct {
	Eid          string           `json:"eid"`
	Nickname     string           `json:"nickname"`
	AccountColor string           `json:"accountColor"`
	Pairs        []ExportFilePair `json:"pairs"`
}

var exportFileRe = regexp.MustCompile(`^(EI\d+)\.(\d{8}_\d{6})\.(csv|xlsx)$`)

// listExportGroups reads {exportsDir}/missions/ and groups files by EID+timestamp.
// Returns nil (not an error) when the directory does not exist.
func listExportGroups(exportsDir string) ([]ExportGroup, error) {
	missionsDir := filepath.Join(exportsDir, "missions")
	entries, err := os.ReadDir(missionsDir)
	if os.IsNotExist(err) {
		return nil, nil
	}
	if err != nil {
		return nil, fmt.Errorf("listExportGroups: %w", err)
	}

	// eid -> timestamp -> pair
	pairsByEid := map[string]map[string]*ExportFilePair{}
	var eidOrder []string

	for _, entry := range entries {
		if entry.IsDir() {
			continue
		}
		m := exportFileRe.FindStringSubmatch(entry.Name())
		if m == nil {
			continue
		}
		eid, ts, ext := m[1], m[2], m[3]
		if _, ok := pairsByEid[eid]; !ok {
			pairsByEid[eid] = map[string]*ExportFilePair{}
			eidOrder = append(eidOrder, eid)
		}
		if _, ok := pairsByEid[eid][ts]; !ok {
			pairsByEid[eid][ts] = &ExportFilePair{
				Timestamp:   ts,
				DisplayDate: formatExportTimestamp(ts),
			}
		}
		fullPath := filepath.Join(missionsDir, entry.Name())
		info, infoErr := entry.Info()
		if infoErr != nil {
			continue
		}
		pair := pairsByEid[eid][ts]
		switch ext {
		case "csv":
			pair.CsvPath = fullPath
			pair.CsvSize = info.Size()
		case "xlsx":
			pair.XlsxPath = fullPath
			pair.XlsxSize = info.Size()
		}
	}

	groups := make([]ExportGroup, 0, len(eidOrder))
	for _, eid := range eidOrder {
		pairs := make([]ExportFilePair, 0, len(pairsByEid[eid]))
		for _, pair := range pairsByEid[eid] {
			pairs = append(pairs, *pair)
		}
		sort.Slice(pairs, func(i, j int) bool {
			return pairs[i].Timestamp > pairs[j].Timestamp // newest first
		})
		groups = append(groups, ExportGroup{Eid: eid, Pairs: pairs})
	}
	return groups, nil
}

func formatExportTimestamp(ts string) string {
	t, err := time.Parse("20060102_150405", ts)
	if err != nil {
		return ts
	}
	return t.Format("2006-01-02 15:04")
}

// pruneExportsForPlayer deletes the oldest timestamp groups for playerId,
// keeping at most keepCount groups. keepCount <= 0 is a no-op.
func pruneExportsForPlayer(exportsDir, playerId string, keepCount int) (deletedCount int, freedBytes int64, err error) {
	if keepCount <= 0 {
		return 0, 0, nil
	}
	groups, err := listExportGroups(exportsDir)
	if err != nil {
		return 0, 0, err
	}
	var playerPairs []ExportFilePair
	for _, g := range groups {
		if g.Eid == playerId {
			playerPairs = g.Pairs // already newest-first
			break
		}
	}
	if len(playerPairs) <= keepCount {
		return 0, 0, nil
	}
	toDelete := playerPairs[keepCount:] // tail = oldest
	for _, pair := range toDelete {
		for _, path := range []string{pair.CsvPath, pair.XlsxPath} {
			if path == "" {
				continue
			}
			if info, statErr := os.Stat(path); statErr == nil {
				freedBytes += info.Size()
			}
			if removeErr := os.Remove(path); removeErr != nil && !os.IsNotExist(removeErr) {
				return deletedCount, freedBytes, fmt.Errorf("pruneExportsForPlayer: %w", removeErr)
			}
			deletedCount++
		}
	}
	return deletedCount, freedBytes, nil
}
