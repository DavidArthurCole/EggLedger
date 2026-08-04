using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace EggLedger.Web.Interop;

public sealed class JsResizeObserver : IAsyncDisposable {
    private const string ModulePath = "./_content/EggLedger.Web/js/resizeObserver.js";
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;
    private IJSObjectReference? _handle;

    public JsResizeObserver(IJSRuntime js) {
        _js = js;
    }

    public async Task ObserveAsync<T>(ElementReference element, DotNetObjectReference<T> dotNetRef, string methodName) where T : class {
        _module = await _js.InvokeAsync<IJSObjectReference>("import", ModulePath);
        _handle = await _module.InvokeAsync<IJSObjectReference>("observe", element, dotNetRef, methodName);
    }

    public async ValueTask DisposeAsync() {
        if (_handle is not null && _module is not null) {
            await _module.InvokeVoidAsync("unobserve", _handle);
            await _handle.DisposeAsync();
        }

        if (_module is not null) {
            await _module.DisposeAsync();
        }
    }
}
