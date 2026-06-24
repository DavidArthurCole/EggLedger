using Microsoft.JSInterop;

namespace EggLedger.Web.Tests.Data;

/// <summary>
/// Records every module invocation forwarded by <c>IndexedDb</c>.
/// Returns a queued canned value when one is enqueued for the identifier, else default.
/// </summary>
public sealed class FakeJsObjectReference : IJSObjectReference {
    public List<(string Identifier, object?[] Args)> Calls { get; } = [];

    private readonly Dictionary<string, Queue<object?>> _canned = [];

    public bool Disposed { get; private set; }

    public void Enqueue(string identifier, object? value) {
        if (!_canned.TryGetValue(identifier, out var queue)) {
            queue = new Queue<object?>();
            _canned[identifier] = queue;
        }

        queue.Enqueue(value);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) {
        Calls.Add((identifier, args ?? []));

        if (_canned.TryGetValue(identifier, out var queue) && queue.Count > 0) {
            return new ValueTask<TValue>((TValue)queue.Dequeue()!);
        }

        return new ValueTask<TValue>(default(TValue)!);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        => InvokeAsync<TValue>(identifier, args);

    public ValueTask DisposeAsync() {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}
