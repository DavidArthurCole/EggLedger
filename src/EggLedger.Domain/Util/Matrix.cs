namespace EggLedger.Domain.Util;

/// <summary>
/// Matrix normalization helpers. C# Domain port of Go util/matrix.go. Pure.
/// </summary>
public static class Matrix
{
    /// <summary>
    /// Normalizes a row-major matrix in-place so each row, column, or the whole
    /// grid sums to 100. vals is laid out as nR rows of nC columns (index
    /// r*nC+c). mode must be "row_pct", "col_pct", or "global_pct"; any other
    /// value is a no-op. Rows/columns that sum to zero are left untouched.
    /// </summary>
    public static void Apply2DPctNormalization(double[] vals, int nR, int nC, string mode)
    {
        if (nR == 0 || nC == 0)
        {
            return;
        }
        switch (mode)
        {
            case "row_pct":
                for (var r = 0; r < nR; r++)
                {
                    double rowSum = 0;
                    for (var c = 0; c < nC; c++)
                    {
                        rowSum += vals[r * nC + c];
                    }
                    if (rowSum > 0)
                    {
                        for (var c = 0; c < nC; c++)
                        {
                            vals[r * nC + c] = vals[r * nC + c] / rowSum * 100;
                        }
                    }
                }
                break;
            case "col_pct":
                for (var c = 0; c < nC; c++)
                {
                    double colSum = 0;
                    for (var r = 0; r < nR; r++)
                    {
                        colSum += vals[r * nC + c];
                    }
                    if (colSum > 0)
                    {
                        for (var r = 0; r < nR; r++)
                        {
                            vals[r * nC + c] = vals[r * nC + c] / colSum * 100;
                        }
                    }
                }
                break;
            case "global_pct":
                double total = 0;
                foreach (var v in vals)
                {
                    total += v;
                }
                if (total > 0)
                {
                    for (var i = 0; i < vals.Length; i++)
                    {
                        vals[i] = vals[i] / total * 100;
                    }
                }
                break;
        }
    }
}
