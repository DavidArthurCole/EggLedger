namespace EggLedger.Domain.Reports;

/// <summary>
/// Shared 2D pivot collection machinery. Port of Go reports/pivot.go. Cells accumulate
/// with +=; with a GROUP BY guaranteeing one row per (row, col) this equals plain assignment.
/// </summary>
internal sealed class PivotAccum {
    private readonly PivotAxis _rows = new();
    private readonly PivotAxis _cols = new();
    private readonly Dictionary<string, Dictionary<string, double>> _cells = new(StringComparer.Ordinal);

    private readonly record struct LabelEntry(string Display, string RawVal);

    private sealed class PivotAxis {
        private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
        public List<LabelEntry> Entries { get; } = [];

        public void Add(string display, string rawVal) {
            if (!_seen.Add(display)) {
                return;
            }
            Entries.Add(new LabelEntry(display, rawVal));
        }
    }

    /// <summary>Registers row/col labels and accumulates val into the cell at their intersection (keyed by display).</summary>
    public void Add(string rowDisplay, string rowRaw, string colDisplay, string colRaw, double val) {
        _rows.Add(rowDisplay, rowRaw);
        _cols.Add(colDisplay, colRaw);
        if (!_cells.TryGetValue(rowDisplay, out var rowCells)) {
            rowCells = new Dictionary<string, double>(StringComparer.Ordinal);
            _cells[rowDisplay] = rowCells;
        }
        rowCells.TryGetValue(colDisplay, out var cur);
        rowCells[colDisplay] = cur + val;
    }

    public readonly record struct Finalized(
        List<string> RowLabels,
        List<string> ColLabels,
        List<string> RawRowLabels,
        List<string> RawColLabels,
        double[] Matrix);

    /// <summary>
    /// Sorts requested axes (LabelSortLess on raw values) and flattens cells into a
    /// row-major matrix (r*nC+c); unsorted axes stay in insertion order. Port of Go pivotAccum.finalize.
    /// </summary>
    public Finalized Finalize(bool sortRows, bool sortCols, string rowGroupBy, string colGroupBy) {
        var rowEntries = _rows.Entries;
        var colEntries = _cols.Entries;

        if (sortRows) {
            StableSort(rowEntries, rowGroupBy);
        }
        if (sortCols) {
            StableSort(colEntries, colGroupBy);
        }

        var nR = rowEntries.Count;
        var nC = colEntries.Count;
        var rowLabels = new List<string>(nR);
        var rawRowLabels = new List<string>(nR);
        foreach (var e in rowEntries) {
            rowLabels.Add(e.Display);
            rawRowLabels.Add(e.RawVal);
        }
        var colLabels = new List<string>(nC);
        var rawColLabels = new List<string>(nC);
        foreach (var e in colEntries) {
            colLabels.Add(e.Display);
            rawColLabels.Add(e.RawVal);
        }

        var matrix = new double[nR * nC];
        for (var r = 0; r < nR; r++) {
            if (!_cells.TryGetValue(rowLabels[r], out var cellRow)) {
                continue;
            }
            for (var c = 0; c < nC; c++) {
                cellRow.TryGetValue(colLabels[c], out var v);
                matrix[(r * nC) + c] = v;
            }
        }
        return new Finalized(rowLabels, colLabels, rawRowLabels, rawColLabels, matrix);
    }

    // Stable sort matching Go's sort.SliceStable with the LabelSortLess comparator.
    private static void StableSort(List<LabelEntry> entries, string groupBy) {
        var ordered = entries
            .Select((e, i) => (e, i))
            .OrderBy(x => x, Comparer<(LabelEntry e, int i)>.Create((a, b) => {
                if (Labels.LabelSortLess(groupBy, a.e.RawVal, b.e.RawVal)) {
                    return -1;
                }
                if (Labels.LabelSortLess(groupBy, b.e.RawVal, a.e.RawVal)) {
                    return 1;
                }
                return a.i.CompareTo(b.i);
            }))
            .Select(x => x.e)
            .ToList();
        entries.Clear();
        entries.AddRange(ordered);
    }
}
