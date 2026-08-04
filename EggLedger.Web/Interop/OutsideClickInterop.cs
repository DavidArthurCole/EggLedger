using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace EggLedger.Web.Interop;

public sealed class OutsideClickRegistration : IAsyncDisposable {

    private static long _nextId;

    private readonly IJSRuntime _js;
    private readonly Func<Task> _onOutsideClick;
    private readonly string _id;
    private DotNetObjectReference<OutsideClickRegistration>? _selfRef;
    private bool _registered;

    public OutsideClickRegistration(IJSRuntime js, Func<Task> onOutsideClick, string? id = null) {
        _js = js;
        _onOutsideClick = onOutsideClick;
        _id = id ?? $"outside-click-{Interlocked.Increment(ref _nextId)}";
    }

    public async Task RegisterAsync(ElementReference element) {
        _selfRef ??= DotNetObjectReference.Create(this);
        await _js.InvokeVoidAsync("outsideClickRegister", _id, element, _selfRef);
        _registered = true;
    }

    public async Task UnregisterAsync() {
        if (!_registered) {
            return;
        }

        _registered = false;
        await _js.InvokeVoidAsync("outsideClickUnregister", _id);
    }

    [JSInvokable]
    public async Task OnOutsideClick() {
        _registered = false;
        await _onOutsideClick();
    }

    public async ValueTask DisposeAsync() {
        if (_registered) {
            await UnregisterAsync();
        }

        _selfRef?.Dispose();
    }

}
