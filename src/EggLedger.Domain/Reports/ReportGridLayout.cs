namespace EggLedger.Domain.Reports;

/// <summary>Resolved position and size of a placed report card.</summary>
public readonly record struct GridCardPos(int Col, int Row, int W, int H, int Idx);

/// <summary>A contiguous run of empty cells in a single grid row that can accept a drop.</summary>
public readonly record struct EmptyZone(int ColStart, int ColEnd, int RowStart, int InsertAfter);

/// <summary>
/// Grid layout geometry for the report dashboard. Port of Vue utils/reportGridLayout.ts;
/// replicates CSS grid auto-placement (row direction, no dense) for card placement, drop zones, and insertion order.
/// </summary>
public static class ReportGridLayout
{
    /// <summary>Number of columns in the report grid.</summary>
    public const int GridCols = 8;

    /// <summary>Gap between grid cells, in pixels.</summary>
    public const int GridGap = 12;

    /// <summary>Clamp a card's requested grid width/height to the valid range.</summary>
    public static (int W, int H) ClampDims(int gridW, int gridH) =>
        (Math.Min(Math.Max(gridW, 1), GridCols), Math.Min(Math.Max(gridH, 1), 8));

    /// <summary>Mark a w x h block of cells starting at (col, row) as occupied.</summary>
    public static void MarkOccupied(HashSet<(int, int)> occupied, int col, int row, int w, int h)
    {
        for (int rr = row; rr < row + h; rr++)
        {
            for (int cc = col; cc < col + w; cc++)
            {
                occupied.Add((cc, rr));
            }
        }
    }

    /// <summary>Whether a w x h block starting at (c, r) fits without overlapping occupied cells.</summary>
    public static bool CellsFit(HashSet<(int, int)> occupied, int c, int r, int w, int h)
    {
        for (int rr = r; rr < r + h; rr++)
        {
            for (int cc = c; cc < c + w; cc++)
            {
                if (occupied.Contains((cc, rr)))
                {
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>Replicates CSS grid auto-placement (row direction, no dense).</summary>
    public static (int Col, int Row) FindPlacement(HashSet<(int, int)> occupied, int w, int h, int col, int row)
    {
        int c = col;
        int r = row;
        while (true)
        {
            if (c + w - 1 > GridCols)
            {
                c = 1;
                r++;
            }
            if (CellsFit(occupied, c, r, w, h))
            {
                return (c, r);
            }
            c++;
        }
    }

    /// <summary>Simulate placing a sequence of defs and return the final cursor + occupancy.</summary>
    public static (int Col, int Row, HashSet<(int, int)> Occupied) SimulatePlacement(IReadOnlyList<ReportDefinition> defs)
    {
        var occupied = new HashSet<(int, int)>();
        int col = 1;
        int row = 1;
        foreach (var def in defs)
        {
            var (w, h) = ClampDims(def.GridW, def.GridH);
            var pos = FindPlacement(occupied, w, h, col, row);
            col = pos.Col;
            row = pos.Row;
            MarkOccupied(occupied, col, row, w, h);
            col += w;
        }
        return (col, row, occupied);
    }

    /// <summary>Build the card positions + occupancy set for a displayed list of defs.</summary>
    public static (List<GridCardPos> CardPositions, HashSet<(int, int)> Occupied) BuildOccupancyFromLayout(
        IReadOnlyList<ReportDefinition> defs)
    {
        var cardPositions = new List<GridCardPos>();
        var occupied = new HashSet<(int, int)>();
        int col = 1;
        int row = 1;
        for (int idx = 0; idx < defs.Count; idx++)
        {
            var def = defs[idx];
            var (w, h) = ClampDims(def.GridW, def.GridH);
            var pos = FindPlacement(occupied, w, h, col, row);
            col = pos.Col;
            row = pos.Row;
            cardPositions.Add(new GridCardPos(col, row, w, h, idx));
            MarkOccupied(occupied, col, row, w, h);
            col += w;
        }
        return (cardPositions, occupied);
    }

    /// <summary>Index of the last card preceding a zone's first cell, for insertion ordering.</summary>
    public static int ZoneInsertAfter(IReadOnlyList<GridCardPos> cardPositions, int targetRow, int runStart)
    {
        int zoneFirst = (targetRow - 1) * GridCols + runStart;
        int best = -1;
        foreach (var cp in cardPositions)
        {
            if ((cp.Row - 1) * GridCols + cp.Col < zoneFirst)
            {
                best = Math.Max(best, cp.Idx);
            }
        }
        return best;
    }

    /// <summary>Resolve the list index at which a dragged card should be inserted to land in a zone.</summary>
    public static int FindInsertIndexForZone(IReadOnlyList<ReportDefinition> defs, EmptyZone zone, int fromIdx)
    {
        if (fromIdx < 0 || fromIdx >= defs.Count)
        {
            return fromIdx;
        }
        var draggedDef = defs[fromIdx];

        var withoutDragged = defs.Where((_, i) => i != fromIdx).ToList();
        var (dw, dh) = ClampDims(draggedDef.GridW, draggedDef.GridH);

        for (int insertPos = 0; insertPos <= withoutDragged.Count; insertPos++)
        {
            var (col, row, occupied) = SimulatePlacement(withoutDragged.Take(insertPos).ToList());
            var pos = FindPlacement(occupied, dw, dh, col, row);
            if (pos.Col == zone.ColStart && pos.Row == zone.RowStart)
            {
                return insertPos;
            }
        }

        // Fallback: use zone.InsertAfter.
        int insertAt = zone.InsertAfter;
        if (fromIdx <= insertAt)
        {
            insertAt--;
        }
        return Math.Max(insertAt + 1, 0);
    }

    /// <summary>Empty drop zones (contiguous empty cell runs) within a single grid row.</summary>
    public static List<EmptyZone> RowEmptyZones(int r, HashSet<(int, int)> occupied, IReadOnlyList<GridCardPos> cardPositions)
    {
        var zones = new List<EmptyZone>();
        int? runStart = null;
        for (int c = 1; c <= GridCols + 1; c++)
        {
            bool isEmpty = c <= GridCols && !occupied.Contains((c, r));
            if (isEmpty && runStart is null)
            {
                runStart = c;
                continue;
            }
            if (!isEmpty && runStart is int start)
            {
                zones.Add(new EmptyZone(start, c, r, ZoneInsertAfter(cardPositions, r, start)));
                runStart = null;
            }
        }
        return zones;
    }

    /// <summary>Compute all empty drop zones across the rows occupied by the given defs.</summary>
    public static List<EmptyZone> ComputeEmptyZones(IReadOnlyList<ReportDefinition> defs)
    {
        var (cardPositions, occupied) = BuildOccupancyFromLayout(defs);
        if (cardPositions.Count == 0)
        {
            return [];
        }
        int maxRow = cardPositions.Max(p => p.Row + p.H - 1);
        var zones = new List<EmptyZone>();
        for (int r = 1; r <= maxRow; r++)
        {
            zones.AddRange(RowEmptyZones(r, occupied, cardPositions));
        }
        return zones;
    }
}
