package main

import (
	"context"
	"encoding/json"
	"fmt"
	"math"
	"os"
	"path/filepath"
	"strconv"

	"github.com/DavidArthurCole/EggLedger/ei"
	"github.com/DavidArthurCole/EggLedger/eiafx"
	"github.com/DavidArthurCole/EggLedger/reportdb"
	"github.com/DavidArthurCole/EggLedger/reports"
	"github.com/pkg/errors"
	log "github.com/sirupsen/logrus"
)

func mennoDurationSecondsFor(shipId, durationId int) float64 {
	if eiafx.Config == nil {
		return 0
	}
	ship := ei.MissionInfo_Spaceship(shipId)
	dur := ei.MissionInfo_DurationType(durationId)
	for _, mp := range eiafx.Config.GetMissionParameters() {
		if mp.GetShip() != ship {
			continue
		}
		for _, d := range mp.GetDurations() {
			if d.GetDurationType() == dur {
				return d.GetSeconds()
			}
		}
	}
	return 0
}

func mennoLevelAdjustedCapacityFor(shipId, durationId, level int) float64 {
	if eiafx.Config == nil {
		return 4
	}
	ship := ei.MissionInfo_Spaceship(shipId)
	dur := ei.MissionInfo_DurationType(durationId)
	for _, mp := range eiafx.Config.GetMissionParameters() {
		if mp.GetShip() != ship {
			continue
		}
		for _, d := range mp.GetDurations() {
			if d.GetDurationType() == dur {
				cap := float64(d.GetCapacity()) + float64(d.GetLevelCapacityBump())*float64(level)
				if cap > 0 {
					return cap
				}
			}
		}
	}
	return 4
}


type mennoMatcher func(item ConfigurationItem, rawVal string) bool

func mennoMatcherFor(groupBy string) mennoMatcher {
	switch groupBy {
	case "ship_type":
		return func(item ConfigurationItem, rawVal string) bool {
			v, err := strconv.Atoi(rawVal)
			if err != nil {
				return false
			}
			return item.ShipConfiguration.ShipType.Id == v
		}
	case "duration_type":
		return func(item ConfigurationItem, rawVal string) bool {
			v, err := strconv.Atoi(rawVal)
			if err != nil {
				return false
			}
			return item.ShipConfiguration.ShipDurationType.Id == v
		}
	case "level":
		return func(item ConfigurationItem, rawVal string) bool {
			v, err := strconv.Atoi(rawVal)
			if err != nil {
				return false
			}
			return item.ShipConfiguration.Level == v
		}
	case "mission_target":
		return func(item ConfigurationItem, rawVal string) bool {
			v, err := strconv.Atoi(rawVal)
			if err != nil {
				return false
			}
			return item.ShipConfiguration.TargetArtifact.Id == v
		}
	case "artifact_name":
		return func(item ConfigurationItem, rawVal string) bool {
			v, err := strconv.Atoi(rawVal)
			if err != nil {
				return false
			}
			return item.ArtifactConfiguration.ArtifactType.Id == v
		}
	case "rarity":
		return func(item ConfigurationItem, rawVal string) bool {
			v, err := strconv.Atoi(rawVal)
			if err != nil {
				return false
			}
			return item.ArtifactConfiguration.ArtifactRarity.Id == v
		}
	case "tier":
		return func(item ConfigurationItem, rawVal string) bool {
			v, err := strconv.Atoi(rawVal)
			if err != nil {
				return false
			}
			return item.ArtifactConfiguration.ArtifactLevel == v
		}
	}
	return nil
}

func familyAFXIDSet(familyWeight string) map[int]bool {
	if familyWeight == "" {
		return nil
	}
	ids, ok := eiafx.FamilyAFXIds[familyWeight]
	if !ok {
		return nil
	}
	set := make(map[int]bool, len(ids))
	for _, id := range ids {
		set[id] = true
	}
	return set
}

func handleExecuteMennoComparison(reportId, rawRowLabelsJSON, rawColLabelsJSON string) string {
	action := fmt.Sprintf("execute menno comparison for report %s", reportId)
	wrap := func(err error) error { return errors.Wrap(err, action) }

	row, err := reportdb.RetrieveReport(context.Background(), reportId)
	if err != nil {
		log.WithError(wrap(err)).Debug("menno comparison: report not found")
		return ""
	}

	def, err := rowToReportDef(*row)
	if err != nil {
		log.WithError(wrap(err)).Warn("menno comparison: failed to convert report row")
		return ""
	}

	if !def.MennoEnabled {
		return ""
	}
	if def.Subject != "artifacts" {
		return ""
	}
	if !reports.MennoComparableGroupBy(def.GroupBy) || !reports.MennoComparableGroupBy(def.SecondaryGroupBy) {
		return ""
	}

	filePath := filepath.Join(_internalDir, "menno-data.json")
	raw, err := os.ReadFile(filePath)
	if err != nil {
		log.WithError(wrap(err)).Debug("menno comparison: menno-data.json not found")
		return ""
	}
	var items []ConfigurationItem
	if err := json.Unmarshal(raw, &items); err != nil {
		log.WithError(wrap(err)).Warn("menno comparison: failed to parse menno-data.json")
		return ""
	}
	if len(items) == 0 {
		return ""
	}

	var rawRowLabels, rawColLabels []string
	if err := json.Unmarshal([]byte(rawRowLabelsJSON), &rawRowLabels); err != nil || len(rawRowLabels) == 0 {
		return ""
	}
	if err := json.Unmarshal([]byte(rawColLabelsJSON), &rawColLabels); err != nil || len(rawColLabels) == 0 {
		return ""
	}

	rowMatcher := mennoMatcherFor(def.GroupBy)
	colMatcher := mennoMatcherFor(def.SecondaryGroupBy)
	if rowMatcher == nil || colMatcher == nil {
		return ""
	}

	familySet := familyAFXIDSet(def.FamilyWeight)

	nR := len(rawRowLabels)
	nC := len(rawColLabels)

	pctMode := def.NormalizeBy
	isPct := pctMode == "row_pct" || pctMode == "col_pct" || pctMode == "global_pct"

	familyDrops := make([]float64, nR*nC)
	estimatedMissions := make([]float64, nR*nC)

	for _, item := range items {
		for r, rawRow := range rawRowLabels {
			if !rowMatcher(item, rawRow) {
				continue
			}
			for c, rawCol := range rawColLabels {
				if !colMatcher(item, rawCol) {
					continue
				}
				idx := r*nC + c
				if !isPct {
					itemCap := mennoLevelAdjustedCapacityFor(
						item.ShipConfiguration.ShipType.Id,
						item.ShipConfiguration.ShipDurationType.Id,
						item.ShipConfiguration.Level,
					)
					estimatedMissions[idx] += float64(item.TotalDrops) / itemCap
				}
				if familySet == nil || familySet[item.ArtifactConfiguration.ArtifactType.Id] {
					w := 1.0
					if def.FamilyWeight != "" {
						if cw := eiafx.CraftingWeights[[2]int{item.ArtifactConfiguration.ArtifactType.Id, item.ArtifactConfiguration.ArtifactLevel}]; cw > 0 {
							w = cw
						}
					}
					familyDrops[idx] += float64(item.TotalDrops) * w
				}
			}
		}
	}

	shipAxis := ""
	durAxis := ""
	if def.GroupBy == "ship_type" {
		shipAxis = "row"
	} else if def.SecondaryGroupBy == "ship_type" {
		shipAxis = "col"
	}
	if def.GroupBy == "duration_type" {
		durAxis = "row"
	} else if def.SecondaryGroupBy == "duration_type" {
		durAxis = "col"
	}

	matrixValues := make([]float64, nR*nC)

	// airtimeMatrixValues holds per-nominal-flight-hour rates. These are only
	// meaningful when both ship and duration appear on axes (so nominal duration
	// can be looked up), and are not applicable for percentage modes.
	canComputeAirtime := shipAxis != "" && durAxis != "" && !isPct
	var airtimeMatrixValues []float64
	if canComputeAirtime {
		airtimeMatrixValues = make([]float64, nR*nC)
	}

	for r, rawRow := range rawRowLabels {
		for c, rawCol := range rawColLabels {
			idx := r*nC + c
			fd := familyDrops[idx]

			if isPct {
				matrixValues[idx] = fd
				continue
			}

			estMissions := estimatedMissions[idx]
			if estMissions <= 0 || math.IsNaN(estMissions) || math.IsInf(estMissions, 0) {
				matrixValues[idx] = 0
				continue
			}

			matrixValues[idx] = fd / estMissions

			if canComputeAirtime {
				var shipId, durId int
				switch shipAxis {
				case "row":
					shipId, _ = strconv.Atoi(rawRow)
				case "col":
					shipId, _ = strconv.Atoi(rawCol)
				}
				switch durAxis {
				case "row":
					durId, _ = strconv.Atoi(rawRow)
				case "col":
					durId, _ = strconv.Atoi(rawCol)
				}
				nominalSecs := mennoDurationSecondsFor(shipId, durId)
				if nominalSecs > 0 {
					airtimeMatrixValues[idx] = fd / estMissions / (nominalSecs / 3600.0)
				}
			}
		}
	}

	if isPct {
		applyMenno2DPctNormalization(matrixValues, nR, nC, pctMode)
	}

	rowLabels := make([]string, nR)
	colLabels := make([]string, nC)
	for i, raw := range rawRowLabels {
		rowLabels[i] = reports.FormatLabel(def.GroupBy, raw)
	}
	for i, raw := range rawColLabels {
		colLabels[i] = reports.FormatLabel(def.SecondaryGroupBy, raw)
	}

	result := reports.ReportResult{
		RowLabels:           rowLabels,
		ColLabels:           colLabels,
		MatrixValues:        matrixValues,
		Is2D:                true,
		IsFloat:             true,
		Weight:              def.Weight,
		RawRowLabels:        rawRowLabels,
		RawColLabels:        rawColLabels,
		AirtimeMatrixValues: airtimeMatrixValues,
	}

	out, err := json.Marshal(result)
	if err != nil {
		log.WithError(wrap(err)).Warn("menno comparison: failed to marshal result")
		return ""
	}
	return string(out)
}

func applyMenno2DPctNormalization(vals []float64, nR, nC int, mode string) {
	if nR == 0 || nC == 0 {
		return
	}
	switch mode {
	case "row_pct":
		for r := 0; r < nR; r++ {
			var rowSum float64
			for c := 0; c < nC; c++ {
				rowSum += vals[r*nC+c]
			}
			if rowSum > 0 {
				for c := 0; c < nC; c++ {
					vals[r*nC+c] = vals[r*nC+c] / rowSum * 100
				}
			}
		}
	case "col_pct":
		for c := 0; c < nC; c++ {
			var colSum float64
			for r := 0; r < nR; r++ {
				colSum += vals[r*nC+c]
			}
			if colSum > 0 {
				for r := 0; r < nR; r++ {
					vals[r*nC+c] = vals[r*nC+c] / colSum * 100
				}
			}
		}
	case "global_pct":
		var total float64
		for _, v := range vals {
			total += v
		}
		if total > 0 {
			for i := range vals {
				vals[i] = vals[i] / total * 100
			}
		}
	}
}
