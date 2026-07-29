using EggLedger.Domain.Util;

namespace EggLedger.Domain.Tests;

public class UtilMatrixTests {
    [Fact]
    public void Apply2DPctNormalization_RowPct() {

        var vals = new double[] { 10, 30, 20, 20 };
        Matrix.Apply2DPctNormalization(vals, 2, 2, "row_pct");
        var rowSums = new[] { vals[0] + vals[1], vals[2] + vals[3] };
        foreach (var s in rowSums) {
            Assert.True(Math.Abs(s - 100) <= 0.01, $"row sum = {s:F2}, want 100");
        }
        Assert.True(Math.Abs(vals[0] - 25) <= 0.01, $"vals[0] = {vals[0]:F2}, want 25.0");
    }

    [Fact]
    public void Apply2DPctNormalization_ColPct() {

        var vals = new double[] { 10, 30, 10, 70 };
        Matrix.Apply2DPctNormalization(vals, 2, 2, "col_pct");
        var col0Sum = vals[0] + vals[2];
        var col1Sum = vals[1] + vals[3];
        Assert.True(Math.Abs(col0Sum - 100) <= 0.01, $"col 0 sum = {col0Sum:F2}, want 100");
        Assert.True(Math.Abs(col1Sum - 100) <= 0.01, $"col 1 sum = {col1Sum:F2}, want 100");
    }

    [Fact]
    public void Apply2DPctNormalization_GlobalPct() {
        var vals = new double[] { 25, 25, 25, 25 };
        Matrix.Apply2DPctNormalization(vals, 2, 2, "global_pct");
        var total = vals[0] + vals[1] + vals[2] + vals[3];
        Assert.True(Math.Abs(total - 100) <= 0.01, $"global sum = {total:F2}, want 100");
        foreach (var v in vals) {
            Assert.True(Math.Abs(v - 25) <= 0.01, $"vals = {v:F2}, want 25.0");
        }
    }

    [Fact]
    public void Apply2DPctNormalization_ZeroRow_NoDivisionByZero() {

        var vals = new double[] { 0, 0, 10, 10 };
        Matrix.Apply2DPctNormalization(vals, 2, 2, "row_pct");
        Assert.True(vals[0] == 0 && vals[1] == 0, $"zero row should remain zero, got [{vals[0]} {vals[1]}]");
    }

    [Fact]
    public void Apply2DPctNormalization_UnknownMode_Noop() {
        var vals = new double[] { 10, 20, 30, 40 };
        var original = new double[] { 10, 20, 30, 40 };
        Matrix.Apply2DPctNormalization(vals, 2, 2, "none");
        for (var i = 0; i < vals.Length; i++) {
            Assert.Equal(original[i], vals[i]);
        }
    }

    [Fact]
    public void Apply2DPctNormalization_EmptyMatrix_Noop() {
        var vals = Array.Empty<double>();
        Matrix.Apply2DPctNormalization(vals, 0, 0, "row_pct");
        Assert.Empty(vals);
    }
}
