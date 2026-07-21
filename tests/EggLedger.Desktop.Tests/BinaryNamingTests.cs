using EggLedger.Desktop.Update;

namespace EggLedger.Desktop.Tests;

public sealed class BinaryNamingTests {
    [Theory]
    [InlineData("EggLedger_new.exe", true)]
    [InlineData("EggLedger_new", true)]
    [InlineData("EggLedger.exe", false)]
    [InlineData("EggLedger", false)]
    [InlineData(@"C:\x\EggLedger_new.exe", true)]
    [InlineData(@"C:\x\EggLedger.exe", false)]
    public void IsNewBinaryName(string path, bool expected)
        => Assert.Equal(expected, BinaryNaming.IsNewBinaryName(path));

    [Fact]
    public void CanonicalPathFromNew_StripsSuffixKeepsExtAndDir() {
        var dir = Path.Combine(Path.GetTempPath(), "egg-canon");
        Assert.Equal(
            Path.Combine(dir, "EggLedger.exe"),
            BinaryNaming.CanonicalPathFromNew(Path.Combine(dir, "EggLedger_new.exe")));
        Assert.Equal(
            Path.Combine(dir, "EggLedger"),
            BinaryNaming.CanonicalPathFromNew(Path.Combine(dir, "EggLedger_new")));
    }

    [Fact]
    public void DecideReplace_NormalNameNoFlags_DoesNotRun() {
        var (run, _, _) = BinaryNaming.DecideReplace(@"C:\app\EggLedger.exe", 0, "");
        Assert.False(run);
    }

    [Fact]
    public void DecideReplace_NewNameNoFlags_RunsWithDerivedPathAndZeroPid() {
        var self = @"C:\app\EggLedger_new.exe";
        var (run, pid, path) = BinaryNaming.DecideReplace(self, 0, "");
        Assert.True(run);
        Assert.Equal(0, pid);
        Assert.Equal(@"C:\app\EggLedger.exe", path);
    }

    [Fact]
    public void DecideReplace_NewNameWithFlags_UsesFlagPathAndPid() {
        var self = @"C:\app\EggLedger_new.exe";
        var flagPath = @"C:\app\Installed\EggLedger.exe";
        var (run, pid, path) = BinaryNaming.DecideReplace(self, 4242, flagPath);
        Assert.True(run);
        Assert.Equal(4242, pid);
        Assert.Equal(flagPath, path);
    }

    [Fact]
    public void DecideReplace_NormalNameWithLegacyFlags_Runs() {
        var self = @"C:\app\EggLedger.exe";
        var flagPath = @"C:\app\Installed\EggLedger.exe";
        var (run, pid, path) = BinaryNaming.DecideReplace(self, 9999, flagPath);
        Assert.True(run);
        Assert.Equal(9999, pid);
        Assert.Equal(flagPath, path);
    }

    [Theory]
    [InlineData(@"C:\app\EggLedger.exe", @"C:\app\EggLedger_new.exe")]
    [InlineData("/opt/egg/EggLedger", "/opt/egg/EggLedger_new")]
    public void NewBinaryTempPath_AppendsSuffix(string exePath, string expectedWindows) {
        var actual = UpdateService.NewBinaryTempPath(exePath);

        var actualBase = Path.GetFileName(actual);
        if (OperatingSystem.IsWindows()) {
            Assert.Equal(Path.GetFileName(expectedWindows.EndsWith(".exe") ? expectedWindows : expectedWindows + ".exe"), actualBase);
        } else {
            Assert.Contains("_new", actualBase);
        }
    }
}
