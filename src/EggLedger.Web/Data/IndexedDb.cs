using Microsoft.JSInterop;

namespace EggLedger.Web.Data;

/// <summary>
/// Forwards <see cref="IIndexedDb"/> calls to the wwwroot/js/indexeddb.js ES module,
/// importing it lazily on first use and caching the reference.
/// </summary>
public sealed class IndexedDb(IJSRuntime js) : IIndexedDb, IAsyncDisposable
{
    private const string ModulePath = "./_content/EggLedger.Web/js/indexeddb.js";

    private readonly IJSRuntime _js = js;

    private IJSObjectReference? _module;

    private async ValueTask<IJSObjectReference> ModuleAsync()
        => _module ??= await _js.InvokeAsync<IJSObjectReference>("import", ModulePath);

    public async ValueTask PutAsync(string store, object value)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("put", store, value);
    }

    public async ValueTask<int> PutManyAsync(string store, IEnumerable<object> values)
    {
        var module = await ModuleAsync();
        return await module.InvokeAsync<int>("putMany", store, values);
    }

    public async ValueTask<T?> GetAsync<T>(string store, object key)
    {
        var module = await ModuleAsync();
        return await module.InvokeAsync<T?>("get", store, key);
    }

    public async ValueTask<T[]> GetAllAsync<T>(string store)
    {
        var module = await ModuleAsync();
        return await module.InvokeAsync<T[]>("getAll", store);
    }

    public async ValueTask<T[]> GetAllByIndexAsync<T>(string store, string index, object value)
    {
        var module = await ModuleAsync();
        return await module.InvokeAsync<T[]>("getAllByIndex", store, index, value);
    }

    public async ValueTask DeleteAsync(string store, object key)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("del", store, key);
    }

    public async ValueTask ClearAsync(string store)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("clear", store);
    }

    public async ValueTask<int> CountAsync(string store)
    {
        var module = await ModuleAsync();
        return await module.InvokeAsync<int>("count", store);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.DisposeAsync();
            _module = null;
        }
    }
}
