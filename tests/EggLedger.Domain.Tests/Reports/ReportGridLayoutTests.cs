using EggLedger.Domain.Reports;

namespace EggLedger.Domain.Tests.Reports;

/// <summary>
/// Golden tests for the grid packing math, mirroring the Vue
/// utils/reportGridLayout.ts behavior (CSS grid auto-placement, row direction).
/// </summary>
public class ReportGridLayoutTests
{
    private static ReportDefinition Def(int w, int h) => new() { GridW = w, GridH = h };

    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(3, 4, 3, 4)]
    [InlineData(99, 99, 8, 8)]
    [InlineData(-2, -5, 1, 1)]
    public void ClampDims_ClampsToRange(int w, int h, int ew, int eh)
    {
        var (cw, ch) = ReportGridLayout.ClampDims(w, h);
        Assert.Equal(ew, cw);
        Assert.Equal(eh, ch);
    }

    [Fact]
    public void BuildOccupancy_PacksRowMajorWrappingAtEightCols()
    {
        // Three 4-wide cards: two fit on row 1 (cols 1 and 5), third wraps to row 2.
        var defs = new[] { Def(4, 2), Def(4, 2), Def(4, 2) };
        var (positions, _) = ReportGridLayout.BuildOccupancyFromLayout(defs);

        Assert.Equal(new GridCardPos(1, 1, 4, 2, 0), positions[0]);
        Assert.Equal(new GridCardPos(5, 1, 4, 2, 1), positions[1]);
        Assert.Equal(new GridCardPos(1, 3, 4, 2, 2), positions[2]);
    }

    [Fact]
    public void BuildOccupancy_FillsGapLeftByTallNeighbor()
    {
        // A 2x2 then a 2x1: card 0 occupies cols 1-2 rows 1-2, card 1 lands at col 3 row 1.
        var defs = new[] { Def(2, 2), Def(2, 1) };
        var (positions, occupied) = ReportGridLayout.BuildOccupancyFromLayout(defs);

        Assert.Equal(new GridCardPos(1, 1, 2, 2, 0), positions[0]);
        Assert.Equal(new GridCardPos(3, 1, 2, 1, 1), positions[1]);
        Assert.Contains((1, 1), occupied);
        Assert.Contains((2, 2), occupied);
        Assert.Contains((3, 1), occupied);
        Assert.DoesNotContain((1, 3), occupied);
    }

    [Fact]
    public void CellsFit_DetectsOverlap()
    {
        var occupied = new HashSet<(int, int)>();
        ReportGridLayout.MarkOccupied(occupied, 1, 1, 2, 2);
        Assert.False(ReportGridLayout.CellsFit(occupied, 2, 2, 2, 2));
        Assert.True(ReportGridLayout.CellsFit(occupied, 3, 1, 2, 2));
    }

    [Fact]
    public void FindPlacement_WrapsToNextRowWhenOverflowing()
    {
        var occupied = new HashSet<(int, int)>();
        // Want a 6-wide card starting at col 5: cannot fit (5+6-1=10 > 8), wraps to row 2 col 1.
        var pos = ReportGridLayout.FindPlacement(occupied, 6, 1, 5, 1);
        Assert.Equal((1, 2), pos);
    }

    [Fact]
    public void ComputeEmptyZones_FindsTrailingGapOnLastRow()
    {
        // One 4-wide card on row 1 leaves cols 5-8 empty.
        var defs = new[] { Def(4, 1) };
        var zones = ReportGridLayout.ComputeEmptyZones(defs);

        Assert.Single(zones);
        Assert.Equal(5, zones[0].ColStart);
        Assert.Equal(9, zones[0].ColEnd);
        Assert.Equal(1, zones[0].RowStart);
        Assert.Equal(0, zones[0].InsertAfter);
    }

    [Fact]
    public void ComputeEmptyZones_EmptyForNoDefs()
    {
        Assert.Empty(ReportGridLayout.ComputeEmptyZones(Array.Empty<ReportDefinition>()));
    }

    [Fact]
    public void FindInsertIndexForZone_PlacesDraggedCardIntoTrailingGap()
    {
        // Layout: [3-wide][3-wide][2-wide]. Removing the 2-wide (idx 2) leaves a
        // trailing gap at cols 7-8 on row 1. Resolving the drag for that zone
        // should map back to an in-range insert position.
        var defs = new[] { Def(3, 1), Def(3, 1), Def(2, 1) };
        var zones = ReportGridLayout.ComputeEmptyZones(defs.Where((_, i) => i != 2).ToList());
        Assert.NotEmpty(zones);
        int insertPos = ReportGridLayout.FindInsertIndexForZone(defs, zones[0], 2);
        Assert.True(insertPos is >= 0 and <= 3);
    }
}
