using Microsoft.AspNetCore.Components;

namespace EggLedger.Web.Services;

/// <summary>Abstraction over the browser redirect used by the OAuth start flow, so the redirect is fakeable in tests.</summary>
public interface INavigation {
    /// <summary>Full external redirect to <paramref name="url"/>.</summary>
    void NavigateTo(string url);
}

public sealed class BlazorNavigation : INavigation {
    private readonly NavigationManager _nav;

    public BlazorNavigation(NavigationManager nav) => _nav = nav;

    public void NavigateTo(string url) => _nav.NavigateTo(url, forceLoad: true);
}
