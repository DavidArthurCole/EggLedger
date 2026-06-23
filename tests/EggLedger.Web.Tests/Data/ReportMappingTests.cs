using EggLedger.Web.Data;

namespace EggLedger.Web.Tests.Data;

/// <summary>
/// Tests the ReportRow &lt;-&gt; ReportDefinition mapping the builder and engine
/// rely on: full round-trip, filters JSON parse/serialize, and the column
/// defaults applied when building a row from a sparse definition.
/// </summary>
public sealed class ReportMappingTests
{
    [Fact]
    public void RoundTrips_AllScalarFields()
    {
        var row = new ReportRow
        {
            Id = "r1",
            AccountId = "EI1",
            Name = "Report",
            Subject = "ships",
            Mode = "aggregate",
            DisplayMode = "bar",
            GroupBy = "ship_type",
            SecondaryGroupBy = "duration_type",
            TimeBucket = "month",
            CustomBucketN = 3,
            CustomBucketUnit = "week",
            Filters = "{\"and\":[{\"topLevel\":\"level\",\"op\":\"=\",\"val\":\"5\"}],\"or\":[]}",
            GridX = 1,
            GridY = 2,
            GridW = 3,
            GridH = 4,
            Weight = "MEDIUM",
            Color = "#abcdef",
            Description = "desc",
            ChartType = "line",
            SortOrder = 7,
            CreatedAt = 100,
            UpdatedAt = 200,
            ValueFilterOp = ">",
            ValueFilterThreshold = 1.5,
            GroupId = "g1",
            NormalizeBy = "row_pct",
            LabelColors = "{\"A\":\"#fff\"}",
            UnfilledColor = "#111111",
            FamilyWeight = "x",
            MennoEnabled = true,
            MennoCompareMode = "overlay",
            MinSampleSize = 12,
        };

        var def = ReportMapping.ToDefinition(row);
        Assert.Equal("ship_type", def.GroupBy);
        Assert.Equal("duration_type", def.SecondaryGroupBy);
        Assert.Single(def.Filters.And);
        Assert.Equal("level", def.Filters.And[0].TopLevel);
        Assert.Equal("5", def.Filters.And[0].Val);
        Assert.True(def.MennoEnabled);

        var back = ReportMapping.ToRow(def);
        Assert.Equal(row.GroupBy, back.GroupBy);
        Assert.Equal(row.SecondaryGroupBy, back.SecondaryGroupBy);
        Assert.Equal(row.NormalizeBy, back.NormalizeBy);
        Assert.Equal(row.Color, back.Color);
        Assert.Equal(row.MinSampleSize, back.MinSampleSize);
        // Filters survive the structured round-trip.
        var reparsed = ReportMapping.ParseFilters(back.Filters);
        Assert.Equal("level", reparsed.And[0].TopLevel);
    }

    [Fact]
    public void ToRow_AppliesColumnDefaultsForBlankFields()
    {
        var def = new EggLedger.Domain.Reports.ReportDefinition { Id = "x" };
        var row = ReportMapping.ToRow(def);
        Assert.Equal("LOW", row.Weight);
        Assert.Equal("#6366f1", row.Color);
        Assert.Equal("bar", row.ChartType);
        Assert.Equal("none", row.NormalizeBy);
        Assert.Equal("side_by_side", row.MennoCompareMode);
    }

    [Fact]
    public void GroupId_SurvivesEditShapedRoundTrip()
    {
        // An edit loads a persisted row, then re-saves it. The builder preserves the
        // report's existing GroupId on edit (no clobber to ""), so the mapping must
        // carry GroupId through ToDefinition -> ToRow unchanged.
        var row = new ReportRow { Id = "r1", Name = "Report", GroupId = "g42" };
        var def = ReportMapping.ToDefinition(row);
        Assert.Equal("g42", def.GroupId);
        var back = ReportMapping.ToRow(def);
        Assert.Equal("g42", back.GroupId);
    }

    [Fact]
    public void ParseFilters_TolerantOfBlankAndInvalid()
    {
        Assert.Empty(ReportMapping.ParseFilters("").And);
        Assert.Empty(ReportMapping.ParseFilters("garbage").Or);
        Assert.Empty(ReportMapping.ParseFilters(null).And);
    }
}
