using EggLedger.Domain.Util;

namespace EggLedger.Domain.Tests;

public class UtilTimeFmtTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void HumanizeTime_JustNow()
    {
        Assert.Equal("just now", TimeFmt.HumanizeTime(Now.AddSeconds(-10), Now));
    }

    [Fact]
    public void HumanizeTime_Minutes()
    {
        Assert.Equal("5 minutes ago", TimeFmt.HumanizeTime(Now.AddMinutes(-5), Now));
    }

    [Fact]
    public void HumanizeTime_Hours()
    {
        Assert.Equal("3 hours ago", TimeFmt.HumanizeTime(Now.AddHours(-3), Now));
    }

    [Fact]
    public void HumanizeTime_Days()
    {
        Assert.Equal("2 days ago", TimeFmt.HumanizeTime(Now.AddHours(-48), Now));
    }

    [Fact]
    public void HumanizeTime_Months()
    {
        // 45 days = 1 month by the formula (45*24 / (24*30) = 1)
        Assert.Equal("1 months ago", TimeFmt.HumanizeTime(Now.AddHours(-45 * 24), Now));
    }

    [Fact]
    public void HumanizeTime_Years()
    {
        Assert.Equal("1 years ago", TimeFmt.HumanizeTime(Now.AddHours(-400 * 24), Now));
    }

    [Fact]
    public void UnixRoundTrip()
    {
        // 1700000000.5 seconds since epoch survives a round trip within sub-ms.
        var unix = 1_700_000_000.5;
        var t = TimeFmt.UnixToTime(unix);
        Assert.True(Math.Abs(TimeFmt.TimeToUnix(t) - unix) < 1e-6);
    }
}
