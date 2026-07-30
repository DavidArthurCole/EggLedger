using EggLedger.Web.Platform;

namespace EggLedger.Desktop.Platform;

public sealed class DesktopTimeZoneProvider : IUserTimeZoneProvider {
    public TimeZoneInfo TimeZone => TimeZoneInfo.Local;

    public Task EnsureUpToDateAsync() => Task.CompletedTask;
}
