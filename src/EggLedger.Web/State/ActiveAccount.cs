namespace EggLedger.Web.State;

/// <summary>Currently selected account id plus a change notification. Scoped (one instance per app in WASM).</summary>
public sealed class ActiveAccount
{
    /// <summary>Selected account id, or null when none is active.</summary>
    public string? ActiveAccountId { get; private set; }

    /// <summary>Raised whenever the active account id changes.</summary>
    public event Action? Changed;

    /// <summary>Set the active account. Treats null/empty as "no account"; no-op when unchanged.</summary>
    public void SetActive(string? id)
    {
        var normalized = string.IsNullOrEmpty(id) ? null : id;
        if (normalized == ActiveAccountId)
        {
            return;
        }

        ActiveAccountId = normalized;
        Changed?.Invoke();
    }
}
