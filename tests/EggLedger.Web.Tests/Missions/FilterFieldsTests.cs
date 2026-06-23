using EggLedger.Web.Missions;

namespace EggLedger.Web.Tests.Missions;

/// <summary>
/// Golden tests for <see cref="FilterFields"/> derived from
/// www/src/utils/filterFields.ts: the field set, default operators, and scope
/// partitioning.
/// </summary>
public sealed class FilterFieldsTests
{
    [Fact]
    public void ReportFilterFields_HasExpectedKeysInOrder()
    {
        var keys = FilterFields.ReportFilterFields.Select(f => f.Key).ToArray();
        Assert.Equal(new[]
        {
            "ship", "duration", "level", "target", "type", "launchDT", "returnDT",
            "dubcap", "buggedcap", "drops",
            "artifact_name", "artifact_rarity", "artifact_tier", "artifact_spec_type", "artifact_quality",
        }, keys);
    }

    [Fact]
    public void GetReportField_FindsByKey()
    {
        Assert.Equal("Ship", FilterFields.GetReportField("ship")!.Label);
        Assert.Null(FilterFields.GetReportField("nope"));
    }

    [Fact]
    public void DefaultOpForField_BoolIsTrue_DropsIsContains_DateIsDayEq_ElseFirst()
    {
        Assert.Equal("true", FilterFields.DefaultOpForField(FilterFields.GetReportField("dubcap")!));
        Assert.Equal("c", FilterFields.DefaultOpForField(FilterFields.GetReportField("drops")!));
        Assert.Equal("=", FilterFields.DefaultOpForField(FilterFields.GetReportField("ship")!));
        // Mission Data bar date "on" must default to "d=" (day-equality), not "=".
        // A date "=" reference-compares and never matches in MissionFilterMatcher.
        Assert.Equal("d=", FilterFields.DefaultOpForField(FilterFields.GetReportField("launchDT")!));
        Assert.Equal("d=", FilterFields.DefaultOpForField(FilterFields.GetReportField("returnDT")!));
    }

    [Fact]
    public void MissionBarOpsFor_DateFields_UseDayEqOnOperator()
    {
        var ops = FilterFields.MissionBarOpsFor(FilterFields.GetReportField("launchDT")!);
        Assert.Equal(new[] { "d=", "<", ">" }, ops.Select(o => o.Value).ToArray());
        // "on" is the day-equality operator the matcher's d= branch handles.
        Assert.Equal("d=", ops.First(o => o.Label == "on").Value);

        // Non-date fields keep their own ops unchanged (Reports field set intact).
        var shipOps = FilterFields.MissionBarOpsFor(FilterFields.GetReportField("ship")!);
        Assert.Same(FilterFields.GetReportField("ship")!.Ops, shipOps);
    }

    [Fact]
    public void Scopes_PartitionMissionAndArtifact()
    {
        Assert.Equal(10, FilterFields.ReportMissionFields().Count);
        Assert.Equal(5, FilterFields.ReportArtifactFields().Count);
    }

    [Fact]
    public void OperatorLists_MatchVue()
    {
        Assert.Equal(6, FilterFields.ComparisonOps.Count);
        Assert.Equal(2, FilterFields.EqualityOps.Count);
        Assert.Equal(5, FilterFields.DateOps.Count);
        Assert.Equal(2, FilterFields.DropsOps.Count);
    }
}
