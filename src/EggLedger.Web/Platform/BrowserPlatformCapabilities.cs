using Microsoft.JSInterop;

namespace EggLedger.Web.Platform;

public sealed class BrowserPlatformCapabilities(IJSRuntime js) : IPlatformCapabilities, IAsyncDisposable {
    private const string ModulePath = "./_content/EggLedger.Web/js/platform.js";
    private readonly IJSRuntime _js = js;
    private IJSObjectReference? _module;

    private async ValueTask<IJSObjectReference> ModuleAsync()
        => _module ??= await _js.InvokeAsync<IJSObjectReference>("import", ModulePath);

    public bool IsDesktop => false;
    public Task OpenFileAsync(string path) => Task.CompletedTask;
    public Task OpenFileInFolderAsync(string path) => Task.CompletedTask;

    public Task OpenUrlAsync(string url) => Task.CompletedTask;

    public Task<string?> ChooseSaveFilePathAsync(string defaultName) => Task.FromResult<string?>(null);

    public async Task RestartAppAsync() {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("reload");
    }

    public async Task<(int w, int h)> GetWindowSizeAsync() {
        var module = await ModuleAsync();
        var dims = await module.InvokeAsync<int[]>("windowSize");
        return (dims[0], dims[1]);
    }

    public Task<string?> ChooseFolderAsync() => Task.FromResult<string?>(null);

    public Task SetFolderHiddenAsync(string path, bool hidden) => Task.CompletedTask;

    public string DataRootDir => "";

    public async ValueTask DisposeAsync() {
        if (_module is not null) {
            await _module.DisposeAsync();
        }
    }
}
