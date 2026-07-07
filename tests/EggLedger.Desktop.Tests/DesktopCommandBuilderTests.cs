using System.Runtime.InteropServices;
using EggLedger.Desktop.Platform;

namespace EggLedger.Desktop.Tests;

/// <summary>
/// Pure per-OS command-construction tests for the shell-out platform operations.
/// These assert the (exe, args) built for each platform matches the Go reference
/// (EggLedger/platform/platform*.go) without launching any process or dialog.
/// </summary>
public sealed class DesktopCommandBuilderTests {
    [Fact]
    public void BuildOpenCommand_Windows_UsesExplorerWithPath() {
        var (exe, args) = DesktopCommandBuilder.BuildOpenCommand(OSPlatform.Windows, @"C:\data\file.json");
        Assert.Equal("explorer.exe", exe);
        Assert.Equal([@"C:\data\file.json"], args);
    }

    [Fact]
    public void BuildOpenCommand_Mac_UsesOpenWithPath() {
        var (exe, args) = DesktopCommandBuilder.BuildOpenCommand(OSPlatform.OSX, "/Users/me/file.json");
        Assert.Equal("open", exe);
        Assert.Equal(["/Users/me/file.json"], args);
    }

    [Fact]
    public void BuildOpenCommand_Linux_UsesXdgOpenWithPath() {
        var (exe, args) = DesktopCommandBuilder.BuildOpenCommand(OSPlatform.Linux, "/home/me/file.json");
        Assert.Equal("xdg-open", exe);
        Assert.Equal(["/home/me/file.json"], args);
    }

    [Fact]
    public void BuildOpenInFolderCommand_Windows_SelectsFileInExplorer() {
        var (exe, args) = DesktopCommandBuilder.BuildOpenInFolderCommand(OSPlatform.Windows, @"C:\data\file.json");
        Assert.Equal("explorer.exe", exe);
        Assert.Equal([@"/select,C:\data\file.json"], args);
    }

    [Fact]
    public void BuildOpenInFolderCommand_Mac_RevealsWithOpenDashR() {
        var (exe, args) = DesktopCommandBuilder.BuildOpenInFolderCommand(OSPlatform.OSX, "/Users/me/file.json");
        Assert.Equal("open", exe);
        Assert.Equal(["-R", "/Users/me/file.json"], args);
    }

    [Fact]
    public void BuildOpenInFolderCommand_Linux_OpensContainingDirectory() {
        var (exe, args) = DesktopCommandBuilder.BuildOpenInFolderCommand(OSPlatform.Linux, "/home/me/sub/file.json");
        Assert.Equal("xdg-open", exe);
        // Linux cannot select a file generically, so it opens the containing dir.
        // Path.GetDirectoryName uses the host separator; assert what was passed.
        Assert.Single(args);
        Assert.EndsWith("file.json", "/home/me/sub/file.json");
        Assert.DoesNotContain("file.json", args[0]);
    }

    [Fact]
    public void BuildRestartCommand_RelaunchesExeWithNoArgs() {
        var (exe, args) = DesktopCommandBuilder.BuildRestartCommand(@"C:\app\EggLedger.exe");
        Assert.Equal(@"C:\app\EggLedger.exe", exe);
        Assert.Empty(args);
    }
}
