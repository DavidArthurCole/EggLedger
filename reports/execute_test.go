package reports

import (
	"testing"
)

func TestGapFill_SparseSeries(t *testing.T) {
	buckets := []string{"2025-01", "2025-02", "2025-03"}
	groups := []string{"Eagle", "Henerprise"}
	cells := map[string]map[string]float64{
		"2025-01": {"Eagle": 5, "Henerprise": 3},
		"2025-02": {"Eagle": 7},
		"2025-03": {"Eagle": 2, "Henerprise": 8},
	}

	nR := len(buckets)
	nC := len(groups)
	matrix := make([]float64, nR*nC)
	for r, bucket := range buckets {
		for c, grp := range groups {
			if cells[bucket] != nil {
				matrix[r*nC+c] = cells[bucket][grp]
			}
		}
	}

	// 2025-02 Henerprise should be 0 (gap-filled)
	if matrix[1*nC+1] != 0 {
		t.Errorf("gap-fill: 2025-02 Henerprise should be 0, got %v", matrix[1*nC+1])
	}
	// 2025-01 Eagle should be 5
	if matrix[0*nC+0] != 5 {
		t.Errorf("2025-01 Eagle should be 5, got %v", matrix[0*nC+0])
	}
	// 2025-03 Henerprise should be 8
	if matrix[2*nC+1] != 8 {
		t.Errorf("2025-03 Henerprise should be 8, got %v", matrix[2*nC+1])
	}
}
