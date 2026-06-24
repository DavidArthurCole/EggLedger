using Microsoft.JSInterop;

namespace EggLedger.Web.Platform;

/// <summary>Browser implementation of <see cref="IPlatformCapabilities"/>. No OS file access: file ops route through downloads, save dialog returns null, restart is a reload. <see cref="IsDesktop"/> is false.</summary>
public sealed class BrowserPlatformCapabilities(IJSRuntime js) : IPlatformCapabilities, IAsyncDisposable {
    private const string ModulePath = "./_content/EggLedger.Web/js/platform.js";

    private readonly IJSRuntime _js = js;

    private IJSObjectReference? _module;

    private async ValueTask<IJSObjectReference> ModuleAsync()
        => _module ??= await _js.InvokeAsync<IJSObjectReference>("import", ModulePath);

    public bool IsDesktop => false;

    /// <summary>No-op: the browser cannot open arbitrary OS files.</summary>
    public Task OpenFileAsync(string path) => Task.CompletedTask;

    /// <summary>No-op: the browser cannot reveal files in the OS file manager.</summary>
    public Task OpenFileInFolderAsync(string path) => Task.CompletedTask;

    /// <summary>Always null: downloads go to the browser's own download path.</summary>
    public Task<string?> ChooseSaveFilePathAsync(string defaultName) => Task.FromResult<string?>(null);

    /// <summary>Reload the page; the closest browser analogue of a restart.</summary>
    public async Task RestartAppAsync() {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("reload");
    }

    /// <summary>Read the viewport size.</summary>
    public async Task<(int w, int h)> GetWindowSizeAsync() {
        var module = await ModuleAsync();
        var dims = await module.InvokeAsync<int[]>("windowSize");
        return (dims[0], dims[1]);
    }

    public async ValueTask DisposeAsync() {
        if (_module is not null) {
            await _module.DisposeAsync();
        }
    }
}
