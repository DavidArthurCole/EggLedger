package main

import (
	"archive/zip"
	"bytes"
	"encoding/csv"
	"fmt"
	"io"
	"os"
	"path/filepath"
	"regexp"
	"time"

	"github.com/pkg/errors"

	"github.com/DavidArthurCole/EggLedger/ei"
	"github.com/DavidArthurCole/EggLedger/xlsxwriter"
)

type mission struct {
	Id               string
	Type             ei.MissionInfo_MissionType
	TypeName         string
	Ship             ei.MissionInfo_Spaceship
	ShipName         string
	DurationType     ei.MissionInfo_DurationType
	DurationTypeName string
	Level            uint32
	LaunchedAt       time.Time
	LaunchedAtStr    string
	ReturnedAt       time.Time
	ReturnedAtStr    string
	Duration         time.Duration
	DurationDays     float64
	Capacity         uint32
	Artifacts        []*ei.ArtifactSpec
	ArtifactNames    []string
	TargetArtifact   ei.ArtifactSpec_Name
}

/*
These two were created to cope with the fact that kevin can't count

This replaces the GetTargetArtifact() method, which for a nil value,
returns LUNAR_TOTEM, million dollar game devlopment right there
*/
func CustomGetTargetArtifact(mission *ei.MissionInfo) ei.ArtifactSpec_Name {
	if mission != nil && mission.TargetArtifact != nil && *mission.StartTimeDerived >= float64(1686260700) {
		return *mission.TargetArtifact
	}
	return ei.ArtifactSpec_UNKNOWN
}

func GetNamedTarget(aspecN *ei.ArtifactSpec_Name) string {
	if aspecN != nil && *aspecN != ei.ArtifactSpec_UNKNOWN {
		return aspecN.CasedName()
	}
	return ""
}

func newMission(r *ei.CompleteMissionResponse) *mission {
	info := r.GetInfo()
	ship := info.GetShip()
	durationType := info.GetDurationType()
	launchedAt := unixToTime(info.GetStartTimeDerived()).Truncate(time.Second)
	durationSeconds := info.GetDurationSeconds()
	duration := time.Duration(durationSeconds) * time.Second
	returnedAt := launchedAt.Add(duration)
	target := CustomGetTargetArtifact(info)
	var artifacts []*ei.ArtifactSpec
	var artifactNames []string
	for _, a := range r.Artifacts {
		artifacts = append(artifacts, a.Spec)
		artifactNames = append(artifactNames, a.Spec.Display())
	}
	return &mission{
		Id:               info.GetIdentifier(),
		Type:             info.GetType(),
		TypeName:         info.GetType().Display(),
		Ship:             ship,
		ShipName:         ship.Name(),
		DurationType:     durationType,
		DurationTypeName: durationType.Display(),
		Level:            info.GetLevel(),
		LaunchedAt:       launchedAt,
		LaunchedAtStr:    launchedAt.Format(time.RFC3339),
		ReturnedAt:       returnedAt,
		ReturnedAtStr:    returnedAt.Format(time.RFC3339),
		Duration:         duration,
		DurationDays:     durationSeconds / 86400,
		Capacity:         info.GetCapacity(),
		Artifacts:        artifacts,
		ArtifactNames:    artifactNames,
		TargetArtifact:   target,
	}
}

// exportColumnDefs lists every fixed export column in order.
// Both CSV and XLSX exporters derive their headers and (for XLSX) column widths from this table.
var exportColumnDefs = []struct {
	header string
	width  float64
}{
	{"ID", 40},
	{"Type", 12},
	{"Ship", 26},
	{"Duration Type", 16},
	{"Level", 8},
	{"Launched at", 22},
	{"Returned at", 22},
	{"Duration days", 16},
	{"Capacity", 10},
	{"Target", 26},
}

func maxArtifactCount(missions []*mission) int {
	var max int
	for _, m := range missions {
		if n := len(m.ArtifactNames); n > max {
			max = n
		}
	}
	return max
}

func exportMissionsToCsv(missions []*mission, path string) error {
	action := fmt.Sprintf("exporting missions to %s", path)
	wrap := func(err error) error {
		return errors.Wrap(err, "error "+action)
	}

	mac := maxArtifactCount(missions)
	header := make([]string, len(exportColumnDefs)+mac)
	for i, col := range exportColumnDefs {
		header[i] = col.header
	}
	for i := 1; i <= mac; i++ {
		header[len(exportColumnDefs)+i-1] = fmt.Sprintf("Artifact %d", i)
	}

	records := [][]string{header}
	for _, m := range missions {
		row := []string{
			m.Id,
			m.TypeName,
			m.ShipName,
			m.DurationTypeName,
			fmt.Sprint(m.Level),
			m.LaunchedAtStr,
			m.ReturnedAtStr,
			fmt.Sprint(m.DurationDays),
			fmt.Sprint(m.Capacity),
			GetNamedTarget(&m.TargetArtifact),
		}
		for i := 0; i < mac; i++ {
			if i < len(m.ArtifactNames) {
				row = append(row, m.ArtifactNames[i])
			} else {
				row = append(row, "")
			}
		}
		records = append(records, row)
	}

	temp, err := writeCsvToTempfile(records, filepath.Dir(path), tempfilePattern(path))
	if err != nil {
		return wrap(err)
	}
	if err := os.Rename(temp, path); err != nil {
		_ = os.Remove(temp)
		return wrap(err)
	}
	return nil
}

func writeCsvToTempfile(records [][]string, dir, pattern string) (temp string, err error) {
	f, err := os.CreateTemp(dir, pattern)
	if err != nil {
		return
	}
	_ = os.Chmod(f.Name(), 0644)
	temp = f.Name()
	defer func() {
		if cerr := f.Close(); cerr != nil && err == nil {
			err = cerr
		}
	}()
	w := csv.NewWriter(f)
	for _, record := range records {
		err = w.Write(record)
		if err != nil {
			return
		}
	}
	w.Flush()
	err = w.Error()
	if err != nil {
		return
	}
	return
}

func exportMissionsToXlsx(missions []*mission, path string) error {
	action := fmt.Sprintf("exporting missions to %s", path)
	wrap := func(err error) error {
		return errors.Wrap(err, "error "+action)
	}

	mac := maxArtifactCount(missions)

	var maxArtifactNameLen int
	for _, m := range missions {
		for _, name := range m.ArtifactNames {
			if n := len(name); n > maxArtifactNameLen {
				maxArtifactNameLen = n
			}
		}
	}
	artifactColWidth := float64(maxArtifactNameLen + 5)

	colWidths := make([]float64, len(exportColumnDefs)+mac)
	for i, col := range exportColumnDefs {
		colWidths[i] = col.width
	}
	for i := 0; i < mac; i++ {
		colWidths[len(exportColumnDefs)+i] = artifactColWidth
	}

	temp, err := os.CreateTemp(filepath.Dir(path), tempfilePattern(path))
	if err != nil {
		return wrap(err)
	}
	_ = os.Chmod(temp.Name(), 0644)
	tempName := temp.Name()

	w, err := xlsxwriter.New(temp)
	if err != nil {
		_ = temp.Close()
		_ = os.Remove(tempName)
		return wrap(err)
	}
	w.SetColWidths(colWidths)

	header := make([]xlsxwriter.Cell, len(exportColumnDefs)+mac)
	for i, col := range exportColumnDefs {
		header[i] = xlsxwriter.StringCell(col.header)
	}
	for i := 1; i <= mac; i++ {
		header[len(exportColumnDefs)+i-1] = xlsxwriter.StringCell(fmt.Sprintf("Artifact %d", i))
	}
	if err := w.WriteRow(header); err != nil {
		_ = temp.Close()
		_ = os.Remove(tempName)
		return wrap(err)
	}

	for _, m := range missions {
		row := []xlsxwriter.Cell{
			xlsxwriter.StringCell(m.Id),
			xlsxwriter.StringCell(m.TypeName),
			xlsxwriter.StringCell(m.ShipName),
			xlsxwriter.StringCell(m.DurationTypeName),
			xlsxwriter.NumberCell(float64(m.Level)),
			xlsxwriter.DatetimeCell(m.LaunchedAt),
			xlsxwriter.DatetimeCell(m.ReturnedAt),
			xlsxwriter.NumberCell(m.DurationDays),
			xlsxwriter.NumberCell(float64(m.Capacity)),
			xlsxwriter.StringCell(GetNamedTarget(&m.TargetArtifact)),
		}
		for i := 0; i < mac; i++ {
			if i < len(m.ArtifactNames) {
				row = append(row, xlsxwriter.StringCell(m.ArtifactNames[i]))
			} else {
				row = append(row, xlsxwriter.StringCell(""))
			}
		}
		if err := w.WriteRow(row); err != nil {
			_ = temp.Close()
			_ = os.Remove(tempName)
			return wrap(err)
		}
	}

	if err := w.Close(); err != nil {
		_ = temp.Close()
		_ = os.Remove(tempName)
		return wrap(err)
	}
	if err := temp.Close(); err != nil {
		_ = os.Remove(tempName)
		return wrap(err)
	}
	if err := os.Rename(tempName, path); err != nil {
		_ = os.Remove(tempName)
		return wrap(err)
	}
	return nil
}

// findLastMatchingFile returns the path of the alphabetically last file in
// directory matching the regexp pattern. Empty string is returned if there's no
// file matching the pattern.
func findLastMatchingFile(directory, pattern string) (string, error) {
	re, err := regexp.Compile(pattern)
	if err != nil {
		return "", err
	}
	entries, err := os.ReadDir(directory)
	if err != nil {
		return "", err
	}
	var last string
	for _, entry := range entries {
		if entry.IsDir() {
			continue
		}
		name := entry.Name()
		if re.MatchString(name) {
			last = name
		}
	}
	if last == "" {
		return "", nil
	}
	return filepath.Join(directory, last), nil
}

// cmpFiles compares two files and returns true if their contents are equal.
// Both files can be fully read into memory; only suitable for small files.
func cmpFiles(path1, path2 string) (bool, error) {
	wrap := func(err error) error {
		return errors.Wrapf(err, "error comparing files %s and %s", path1, path2)
	}
	s1, err := os.Stat(path1)
	if err != nil {
		return false, wrap(err)
	}
	s2, err := os.Stat(path2)
	if err != nil {
		return false, wrap(err)
	}
	if s1.Size() != s2.Size() {
		return false, nil
	}
	b1, err := os.ReadFile(path1)
	if err != nil {
		return false, wrap(err)
	}
	b2, err := os.ReadFile(path2)
	if err != nil {
		return false, wrap(err)
	}
	return bytes.Equal(b1, b2), nil
}

// cmpZipFiles compares two zip files and returns true if their contents are
// equal. This is needed since zip is not deterministic given the exact same
// source files.
func cmpZipFiles(path1, path2 string) (bool, error) {
	wrap := func(err error) error {
		return errors.Wrapf(err, "error comparing zip files %s and %s", path1, path2)
	}
	files1, err := readZipContent(path1)
	if err != nil {
		return false, wrap(err)
	}
	files2, err := readZipContent(path2)
	if err != nil {
		return false, wrap(err)
	}
	if len(files1) != len(files2) {
		return false, nil
	}
	for name, content1 := range files1 {
		content2, ok := files2[name]
		if !ok {
			return false, nil
		}
		if !bytes.Equal(content1, content2) {
			return false, nil
		}
	}
	return true, nil
}

// readZipContent returns a map from file names to file contents for all files
// in the zip.
func readZipContent(path string) (map[string][]byte, error) {
	r, err := zip.OpenReader(path)
	if err != nil {
		return nil, err
	}
	defer r.Close()
	files := make(map[string][]byte)
	for _, f := range r.File {
		rc, err := f.Open()
		if err != nil {
			return files, err
		}
		body, err := io.ReadAll(rc)
		if err != nil {
			return files, err
		}
		if err := rc.Close(); err != nil {
			return files, err
		}
		files[f.Name] = body
	}
	return files, nil
}

func filenameWithoutExt(f string) string {
	f = filepath.Base(f)
	ext := filepath.Ext(f)
	return f[:len(f)-len(ext)]
}

func tempfilePattern(f string) string {
	f = filepath.Base(f)
	ext := filepath.Ext(f)
	return f[:len(f)-len(ext)] + ".*" + ext
}
