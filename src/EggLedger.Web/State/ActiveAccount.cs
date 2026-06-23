namespace EggLedger.Web.State;

/// <summary>
/// Currently selected account id. C# port of the Vue useActiveAccount composable:
/// a single nullable id plus a change notification so the account header and the
/// tab views react to selection. Scoped (one instance per app in WASM).
/// </summary>
public sealed class ActiveAccount
{
    private string? _activeAccountId;

    /// <summary>Selected account id, or null when none is active.</summary>
    public string? ActiveAccountId => _activeAccountId;

    /// <summary>Raised whenever the active account id changes.</summary>
    public event Action? Changed;

    /// <summary>
    /// Set the active account. Treats null/empty as "no account". No-op (and no
    /// notification) when the value is unchanged.
    /// </summary>
    public void SetActive(string? id)
    {
        var normalized = string.IsNullOrEmpty(id) ? null : id;
        if (normalized == _activeAccountId)
        {
            return;
        }

        _activeAccountId = normalized;
        Changed?.Invoke();
    }
}
