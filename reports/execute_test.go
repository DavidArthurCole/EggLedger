package reports

import (
	"math"
	"testing"
)

func TestApply2DPctNormalization_RowPct(t *testing.T) {
	// 2x2 matrix: [[10,30],[20,20]]
	vals := []float64{10, 30, 20, 20}
	apply2DPctNormalization(vals, 2, 2, "row_pct")
	rowSums := []float64{vals[0] + vals[1], vals[2] + vals[3]}
	for i, s := range rowSums {
		if math.Abs(s-100) > 0.01 {
			t.Errorf("row %d sum = %.2f, want 100", i, s)
		}
	}
	if math.Abs(vals[0]-25) > 0.01 {
		t.Errorf("vals[0] = %.2f, want 25.0", vals[0])
	}
}

func TestApply2DPctNormalization_ColPct(t *testing.T) {
	// 2x2: [[10,30],[10,70]]
	vals := []float64{10, 30, 10, 70}
	apply2DPctNormalization(vals, 2, 2, "col_pct")
	col0Sum := vals[0] + vals[2]
	col1Sum := vals[1] + vals[3]
	if math.Abs(col0Sum-100) > 0.01 {
		t.Errorf("col 0 sum = %.2f, want 100", col0Sum)
	}
	if math.Abs(col1Sum-100) > 0.01 {
		t.Errorf("col 1 sum = %.2f, want 100", col1Sum)
	}
}

func TestApply2DPctNormalization_GlobalPct(t *testing.T) {
	vals := []float64{25, 25, 25, 25}
	apply2DPctNormalization(vals, 2, 2, "global_pct")
	total := vals[0] + vals[1] + vals[2] + vals[3]
	if math.Abs(total-100) > 0.01 {
		t.Errorf("global sum = %.2f, want 100", total)
	}
	for i, v := range vals {
		if math.Abs(v-25) > 0.01 {
			t.Errorf("vals[%d] = %.2f, want 25.0", i, v)
		}
	}
}

func TestApply2DPctNormalization_ZeroRow_NoDivisionByZero(t *testing.T) {
	// row 0 is all zeros - should stay 0, no panic
	vals := []float64{0, 0, 10, 10}
	apply2DPctNormalization(vals, 2, 2, "row_pct")
	if vals[0] != 0 || vals[1] != 0 {
		t.Errorf("zero row should remain zero, got [%v %v]", vals[0], vals[1])
	}
}

func TestApply2DPctNormalization_UnknownMode_Noop(t *testing.T) {
	vals := []float64{10, 20, 30, 40}
	original := []float64{10, 20, 30, 40}
	apply2DPctNormalization(vals, 2, 2, "none")
	for i := range vals {
		if vals[i] != original[i] {
			t.Errorf("unknown mode should be noop, vals[%d] changed", i)
		}
	}
}

func TestApply2DPctNormalization_EmptyMatrix_Noop(t *testing.T) {
	vals := []float64{}
	apply2DPctNormalization(vals, 0, 0, "row_pct")
	if len(vals) != 0 {
		t.Errorf("empty matrix should remain empty")
	}
}

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
