using Microsoft.AspNetCore.Components;

namespace EggLedger.Web.Services;

/// <summary>
/// Abstraction over the browser redirect used by the Discord OAuth start flow.
/// The service depends on this rather than <see cref="NavigationManager"/>
/// directly so the redirect is fakeable in tests.
/// </summary>
public interface INavigation
{
    /// <summary>Navigates the browser to <paramref name="url"/> (a full external redirect).</summary>
    void NavigateTo(string url);
}

/// <summary>Blazor-backed <see cref="INavigation"/> over <see cref="NavigationManager"/>.</summary>
public sealed class BlazorNavigation : INavigation
{
    private readonly NavigationManager _nav;

    public BlazorNavigation(NavigationManager nav) => _nav = nav;

    public void NavigateTo(string url) => _nav.NavigateTo(url, forceLoad: true);
}
