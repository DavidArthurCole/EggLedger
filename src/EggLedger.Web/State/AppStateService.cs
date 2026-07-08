using EggLedger.Domain.MissionQuery;
using EggLedger.Web.Services;

namespace EggLedger.Web.State;

/// <summary>Broad app/UI state. Named to avoid the clash with the fetch pipeline enum <see cref="EggLedger.Web.Services.AppState"/>.</summary>
public sealed class AppStateService {
    public string AppVersion {
        get;
        set => Set(ref field, value);
    } = "";

    public IReadOnlyList<KnownAccount> KnownAccounts {
        get;
        set => Set(ref field, value);
    } = [];

    /// <summary>Active tab label, matching the tab bar labels.</summary>
    public string ActiveTab {
        get;
        set => Set(ref field, value);
    } = "Mission Data";

    /// <summary>Current fetch pipeline state, or null before the first fetch.</summary>
    public AppState? PipelineState {
        get;
        set => Set(ref field, value);
    }

    public event Action? Changed;

    private void Set<T>(ref T field, T value) {
        if (EqualityComparer<T>.Default.Equals(field, value)) {
            return;
        }

        field = value;
        Changed?.Invoke();
    }
}
