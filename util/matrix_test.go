package util

import (
	"math"
	"testing"
)

func TestApply2DPctNormalization_RowPct(t *testing.T) {
	// 2x2 matrix: [[10,30],[20,20]]
	vals := []float64{10, 30, 20, 20}
	Apply2DPctNormalization(vals, 2, 2, "row_pct")
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
	Apply2DPctNormalization(vals, 2, 2, "col_pct")
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
	Apply2DPctNormalization(vals, 2, 2, "global_pct")
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
	Apply2DPctNormalization(vals, 2, 2, "row_pct")
	if vals[0] != 0 || vals[1] != 0 {
		t.Errorf("zero row should remain zero, got [%v %v]", vals[0], vals[1])
	}
}

func TestApply2DPctNormalization_UnknownMode_Noop(t *testing.T) {
	vals := []float64{10, 20, 30, 40}
	original := []float64{10, 20, 30, 40}
	Apply2DPctNormalization(vals, 2, 2, "none")
	for i := range vals {
		if vals[i] != original[i] {
			t.Errorf("unknown mode should be noop, vals[%d] changed", i)
		}
	}
}

func TestApply2DPctNormalization_EmptyMatrix_Noop(t *testing.T) {
	vals := []float64{}
	Apply2DPctNormalization(vals, 0, 0, "row_pct")
	if len(vals) != 0 {
		t.Errorf("empty matrix should remain empty")
	}
}
