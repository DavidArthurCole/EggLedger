using EggLedger.Domain.MissionQuery;
using EggLedger.Web.Data;

namespace EggLedger.Web.State;

/// <summary>
/// Single source of truth for loading persisted accounts into shared shell
/// state. Reads the known accounts and active-account id from the
/// <see cref="IndexedDbAccountStore"/> into <see cref="AppStateService"/> +
/// <see cref="ActiveAccount"/> once, and persists the active id whenever the
/// selection changes (whether from the in-tab selector or the global header).
/// C# stand-in for the Go startup that seeds knownAccounts + getActiveAccountId
/// and the setActiveAccountId binding.
/// </summary>
public sealed class AccountLoader : IDisposable
{
    private readonly IndexedDbAccountStore _store;
    private readonly AppStateService _appState;
    private readonly ActiveAccount _active;

    private bool _loaded;
    private bool _subscribed;
    private bool _persisting;

    public AccountLoader(IndexedDbAccountStore store, AppStateService appState, ActiveAccount active)
    {
        _store = store;
        _appState = appState;
        _active = active;
    }

    /// <summary>The full account list last loaded, richest form (SE/PE/TE included).</summary>
    public IReadOnlyList<AccountInfo> Accounts { get; private set; } = [];

    /// <summary>
    /// Loads accounts + active id into shared state on first call and wires the
    /// persist-on-change subscription. Idempotent: later calls re-read the list
    /// (so a freshly added account shows up) without re-subscribing.
    /// </summary>
    public async Task EnsureLoadedAsync()
    {
        if (!_subscribed)
        {
            _active.Changed += OnActiveChanged;
            _subscribed = true;
        }

        await RefreshAsync().ConfigureAwait(false);

        if (!_loaded)
        {
            var activeId = await _store.GetActiveAccountIdAsync().ConfigureAwait(false);
            if (!string.IsNullOrEmpty(activeId))
            {
                _persisting = true;
                _active.SetActive(activeId);
                _persisting = false;
            }
            _loaded = true;
        }
    }

    /// <summary>Re-reads the account list into <see cref="AppStateService"/>.</summary>
    public async Task RefreshAsync()
    {
        Accounts = await _store.GetKnownAccountsAsync().ConfigureAwait(false);
        _appState.KnownAccounts = Accounts.Select(a => a.ToKnownAccount()).ToList();
    }

    private void OnActiveChanged()
    {
        if (_persisting)
        {
            return;
        }
        // Fire-and-forget persist; the in-memory state is already updated.
        _ = _store.SetActiveAccountIdAsync(_active.ActiveAccountId ?? "");
    }

    public void Dispose()
    {
        if (_subscribed)
        {
            _active.Changed -= OnActiveChanged;
            _subscribed = false;
        }
    }
}
