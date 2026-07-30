using EggLedger.Web.Platform;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace EggLedger.Web.Server.Platform;

public sealed class BrowserTimeZoneProvider(
    IHttpContextAccessor httpContextAccessor,
    IJSRuntime js,
    NavigationManager nav) : IUserTimeZoneProvider {
    private const string ModulePath = "./_content/EggLedger.Web/js/timezone.js";
    private readonly bool _hadCookie = httpContextAccessor.HttpContext?.Request.Cookies.ContainsKey("tz") ?? true;

    public TimeZoneInfo TimeZone { get; } =
        Resolve(httpContextAccessor.HttpContext?.Request.Cookies["tz"]);

    public async Task EnsureUpToDateAsync() {
        if (_hadCookie) {
            return;
        }

        var module = await js.InvokeAsync<IJSObjectReference>("import", ModulePath);
        var didSet = await module.InvokeAsync<bool>("ensureCookie");
        if (didSet) {
            nav.NavigateTo(nav.Uri, forceLoad: true);
        }
    }

    private static TimeZoneInfo Resolve(string? id) {
        if (string.IsNullOrEmpty(id)) {
            return TimeZoneInfo.Utc;
        }
        try {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        } catch (TimeZoneNotFoundException) {
            return TimeZoneInfo.Utc;
        } catch (InvalidTimeZoneException) {
            return TimeZoneInfo.Utc;
        }
    }
}
