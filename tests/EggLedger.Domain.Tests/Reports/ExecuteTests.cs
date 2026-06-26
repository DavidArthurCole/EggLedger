using EggLedger.Domain.Reports;

namespace EggLedger.Domain.Tests.Reports;

// Port of Go reports/execute_test.go (gap-fill matrix sanity), plus end-to-end
// ExecuteReport coverage against an in-memory IMissionDb double.
public class ExecuteTests {
    // Direct port of TestGapFill_SparseSeries: build a buckets x groups matrix and
    // verify missing cells are zero-filled and present cells carry through.
    [Fact]
    public void GapFill_SparseSeries() {
        var buckets = new[] { "2025-01", "2025-02", "2025-03" };
        var groups = new[] { "Eagle", "Henerprise" };
        var cells = new Dictionary<string, Dictionary<string, double>> {
            ["2025-01"] = new() { ["Eagle"] = 5, ["Henerprise"] = 3 },
            ["2025-02"] = new() { ["Eagle"] = 7 },
            ["2025-03"] = new() { ["Eagle"] = 2, ["Henerprise"] = 8 },
        };

        var nR = buckets.Length;
        var nC = groups.Length;
        var matrix = new double[nR * nC];
        for (var r = 0; r < nR; r++) {
            for (var c = 0; c < nC; c++) {
                if (cells.TryGetValue(buckets[r], out var row) && row.TryGetValue(groups[c], out var v)) {
                    matrix[(r * nC) + c] = v;
                }
            }
        }

        Assert.Equal(0, matrix[(1 * nC) + 1]); // 2025-02 Henerprise gap-filled
        Assert.Equal(5, matrix[(0 * nC) + 0]); // 2025-01 Eagle
        Assert.Equal(8, matrix[(2 * nC) + 1]); // 2025-03 Henerprise
    }

    private sealed class FakeDb : IMissionDb {
        private readonly Dictionary<string, IReadOnlyList<object?[]>> _byPrefix = new(StringComparer.Ordinal);

        public List<(string sql, IReadOnlyList<object?> args)> Calls { get; } = [];

        // Match on a unique substring of the query so tests need not reproduce whitespace.
        public FakeDb On(string contains, IReadOnlyList<object?[]> rows) {
            _byPrefix[contains] = rows;
            return this;
        }

        public IReadOnlyList<object?[]> Query(string sql, IReadOnlyList<object?> args) {
            Calls.Add((sql, args));
            foreach (var (key, rows) in _byPrefix) {
                if (sql.Contains(key, StringComparison.Ordinal)) {
                    return rows;
                }
            }
            return Array.Empty<object?[]>();
        }
    }

    private sealed class NoWeights : IWeightData {
        public double CraftingWeight(long artifactId, long level) => 1;
        public IReadOnlyList<int> FamilyAfxIds(string familyId) => Array.Empty<int>();
    }

    private sealed class FixedWeights : IWeightData {
        public double CraftingWeight(long artifactId, long level) => 1;
        public IReadOnlyList<int> FamilyAfxIds(string familyId) => new[] { 1, 2 };
    }

    [Fact]
    public void ExecuteReport_Aggregate_FormatsShipLabels() {
        // ship_type aggregate: raw ship enum values -> ship names via FormatLabel.
        var db = new FakeDb().On("GROUP BY m.ship", new object?[][]
        {
            ["9", 10L],
            ["3", 4L],
        });
        var ex = new ReportExecutor(db, new NoWeights());
        var def = new ReportDefinition { Mode = "aggregate", GroupBy = "ship_type", Subject = "missions", AccountId = "EI1" };

        var result = ex.ExecuteReport(def);

        Assert.False(result.Is2D);
        Assert.False(result.IsFloat);
        Assert.Equal([10, 4], result.Values);
        // ship 9 -> Henerprise, ship 3 -> BCR.
        Assert.Equal("Henerprise", result.Labels[0]);
        Assert.Equal("BCR", result.Labels[1]);
        Assert.Equal("EI1", db.Calls[0].args[0]);
    }

    [Fact]
    public void ExecuteReport_Pivot_ProducesSortedMatrix() {
        var db = new FakeDb().On("GROUP BY m.ship, m.duration_type", new object?[][]
        {
            ["3", "1", 2L],
            ["9", "0", 5L],
            ["9", "1", 7L],
        });
        var ex = new ReportExecutor(db, new NoWeights());
        var def = new ReportDefinition {
            Mode = "aggregate",
            GroupBy = "ship_type",
            SecondaryGroupBy = "duration_type",
            AccountId = "EI1",
        };

        var result = ex.ExecuteReport(def);

        Assert.True(result.Is2D);
        // Rows sorted by raw ship value: 3 (BCR) then 9 (Henerprise).
        Assert.Equal(["BCR", "Henerprise"], result.RowLabels);
        // Cols sorted by raw duration value: 0 (Short) then 1 (Standard).
        Assert.Equal(["Short", "Standard"], result.ColLabels);
        // Matrix row-major: [BCR/Short=0, BCR/Standard=2, Hen/Short=5, Hen/Standard=7].
        Assert.Equal([0, 2, 5, 7], result.MatrixValues);
    }

    [Fact]
    public void ExecuteReport_FamilyWeighted_UsesWeightedPath() {
        // FamilyWeight set + non-empty FamilyAfxIds routes to the weighted aggregate.
        // Oracle rows: rawLabel, artifactId, level, capWeight.
        var db = new FakeDb().On("cap_weight", new object?[][]
        {
            ["9", 1L, 0L, 2.0],
            ["3", 2L, 0L, 1.0],
        });
        var ex = new ReportExecutor(db, new FixedWeights());
        var def = new ReportDefinition {
            Subject = "artifacts",
            Mode = "aggregate",
            GroupBy = "ship_type",
            FamilyWeight = "tachyon-stone",
            AccountId = "EI1",
        };

        var result = ex.ExecuteReport(def);

        Assert.True(result.IsFloat);
        // capWeight * craftingWeight(=1); values sorted descending: 2.0 then 1.0.
        Assert.Equal([2.0, 1.0], result.FloatValues);
    }

    [Fact]
    public void ExecuteReport_UnknownMode_Throws() {
        var ex = new ReportExecutor(new FakeDb(), new NoWeights());
        var def = new ReportDefinition { Mode = "bogus", AccountId = "EI1" };
        Assert.Throws<InvalidOperationException>(() => ex.ExecuteReport(def));
    }
}
