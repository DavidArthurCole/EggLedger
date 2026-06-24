namespace EggLedger.Web.State;

// Whether the current cloud session is an admin (gates the Admin tab). Resolved
// once at shell startup from the sync server; raises Changed so the tab appears.
public sealed class AdminState {
    public bool IsAdmin {
        get;
        set {
            if (field == value) return;
            field = value;
            Changed?.Invoke();
        }
    }

    public event Action? Changed;
}
