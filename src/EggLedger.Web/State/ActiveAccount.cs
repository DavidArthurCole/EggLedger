namespace EggLedger.Web.State;

public sealed class ActiveAccount {
    /// <summary>Selected account id, or null when none is active.</summary>
    public string? ActiveAccountId { get; private set; }

    public event Action? Changed;

    /// <summary>Treats null/empty as "no account"; no-op when unchanged.</summary>
    public void SetActive(string? id) {
        var normalized = string.IsNullOrEmpty(id) ? null : id;
        if (normalized == ActiveAccountId) {
            return;
        }

        ActiveAccountId = normalized;
        Changed?.Invoke();
    }
}
