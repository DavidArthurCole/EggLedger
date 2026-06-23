using EggLedger.Domain.MissionQuery;
using EggLedger.Web.Services;

namespace EggLedger.Web.State;

/// <summary>
/// Broad app/UI state. C# port of the Vue useAppState composable (NOT the fetch
/// pipeline enum, which is <see cref="EggLedger.Web.Services.AppState"/> in
/// FetchModels). Named distinctly to avoid that clash. Holds the app version, the
/// known-accounts list, the active tab label, and the current pipeline state, with
/// a change notification so the shell re-renders. Scoped (one instance per app).
/// </summary>
public sealed class AppStateService
{
    private string _appVersion = "";
    private IReadOnlyList<KnownAccount> _knownAccounts = [];
    private string _activeTab = "Ledger";
    private AppState? _pipelineState;

    /// <summary>Compiled app version string.</summary>
    public string AppVersion
    {
        get => _appVersion;
        set => Set(ref _appVersion, value);
    }

    /// <summary>Known accounts loaded from the mission store.</summary>
    public IReadOnlyList<KnownAccount> KnownAccounts
    {
        get => _knownAccounts;
        set => Set(ref _knownAccounts, value);
    }

    /// <summary>Active tab label (matches the tab bar labels).</summary>
    public string ActiveTab
    {
        get => _activeTab;
        set => Set(ref _activeTab, value);
    }

    /// <summary>
    /// Current fetch pipeline state, or null before the first fetch. Reuses the
    /// existing <see cref="AppState"/> enum rather than redefining it.
    /// </summary>
    public AppState? PipelineState
    {
        get => _pipelineState;
        set => Set(ref _pipelineState, value);
    }

    /// <summary>Raised whenever any tracked field changes.</summary>
    public event Action? Changed;

    private void Set<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        Changed?.Invoke();
    }
}
