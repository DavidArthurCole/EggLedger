package util

// Apply2DPctNormalization normalizes a row-major matrix in-place so each row,
// column, or the whole grid sums to 100. vals is laid out as nR rows of nC
// columns (index r*nC+c). mode must be "row_pct", "col_pct", or "global_pct";
// any other value is a no-op. Rows/columns that sum to zero are left untouched.
func Apply2DPctNormalization(vals []float64, nR, nC int, mode string) {
	if nR == 0 || nC == 0 {
		return
	}
	switch mode {
	case "row_pct":
		for r := range nR {
			var rowSum float64
			for c := range nC {
				rowSum += vals[r*nC+c]
			}
			if rowSum > 0 {
				for c := range nC {
					vals[r*nC+c] = vals[r*nC+c] / rowSum * 100
				}
			}
		}
	case "col_pct":
		for c := range nC {
			var colSum float64
			for r := range nR {
				colSum += vals[r*nC+c]
			}
			if colSum > 0 {
				for r := range nR {
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
