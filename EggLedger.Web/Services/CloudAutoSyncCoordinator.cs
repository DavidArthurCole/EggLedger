namespace EggLedger.Web.Services;

public sealed class CloudAutoSyncCoordinator {
    public event Func<Task>? Triggered;

    public async Task NotifySettingsChangedAsync() {
        if (Triggered is not null) {
            await Triggered.Invoke();
        }
    }
}
