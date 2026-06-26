using EggLedger.Domain.MissionQuery;
using EggLedger.Web.Data;

namespace EggLedger.Web.State;

/// <summary>Loads persisted accounts + active id into shared state once, persisting the active id on change.</summary>
public sealed class AccountLoader : IDisposable {
    private readonly IndexedDbAccountStore _store;
    private readonly AppStateService _appState;
    private readonly ActiveAccount _active;

    private bool _loaded;
    private bool _subscribed;
    private bool _persisting;

    public AccountLoader(IndexedDbAccountStore store, AppStateService appState, ActiveAccount active) {
        _store = store;
        _appState = appState;
        _active = active;
    }

    /// <summary>Richest form of the last-loaded list (SE/PE/TE included).</summary>
    public IReadOnlyList<AccountInfo> Accounts { get; private set; } = [];

    /// <summary>Idempotent: later calls re-read the list without re-subscribing.</summary>
    public async Task EnsureLoadedAsync() {
        if (!_subscribed) {
            _active.Changed += OnActiveChanged;
            _subscribed = true;
        }

        await RefreshAsync().ConfigureAwait(false);

        if (!_loaded) {
            var activeId = await _store.GetActiveAccountIdAsync().ConfigureAwait(false);
            if (!string.IsNullOrEmpty(activeId)) {
                _persisting = true;
                _active.SetActive(activeId);
                _persisting = false;
            }
            _loaded = true;
        }
    }

    public async Task RefreshAsync() {
        Accounts = await _store.GetKnownAccountsAsync().ConfigureAwait(false);
        _appState.KnownAccounts = Accounts.Select(a => a.ToKnownAccount()).ToList();
    }

    private void OnActiveChanged() {
        if (_persisting) {
            return;
        }
        // Fire-and-forget; in-memory state is already updated.
        _ = _store.SetActiveAccountIdAsync(_active.ActiveAccountId ?? "");
    }

    public void Dispose() {
        if (_subscribed) {
            _active.Changed -= OnActiveChanged;
            _subscribed = false;
        }
    }
}
