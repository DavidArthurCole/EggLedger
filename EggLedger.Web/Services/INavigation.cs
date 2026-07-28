using Microsoft.AspNetCore.Components;

namespace EggLedger.Web.Services;

public interface INavigation {
    void NavigateTo(string url);
}

public sealed class BlazorNavigation : INavigation {
    private readonly NavigationManager _nav;
    public BlazorNavigation(NavigationManager nav) => _nav = nav;
    public void NavigateTo(string url) => _nav.NavigateTo(url, forceLoad: true);
}
