namespace EggLedger.Web.Platform;

public interface IUserTimeZoneProvider {
    TimeZoneInfo TimeZone { get; }

    Task EnsureUpToDateAsync();
}
