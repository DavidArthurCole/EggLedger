using Microsoft.JSInterop;

namespace EggLedger.Web.Tests.Data;

public sealed class FakeJsRuntime : IJSRuntime {
    private readonly FakeJsObjectReference _module;
    public FakeJsRuntime(FakeJsObjectReference module) => _module = module;
    public List<(string Identifier, object?[] Args)> Calls { get; } = [];

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) {
        Calls.Add((identifier, args ?? []));

        if (identifier == "import") {
            return new ValueTask<TValue>((TValue)(object)_module);
        }

        return new ValueTask<TValue>(default(TValue)!);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        => InvokeAsync<TValue>(identifier, args);
}
