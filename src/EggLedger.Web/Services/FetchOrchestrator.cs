using EggLedger.Web.State;
using Microsoft.Extensions.Logging;

namespace EggLedger.Web.Services;

/// <summary>Owns fetch execution and live progress at circuit scope, so a fetch survives navigating away from whatever page started it. Single fetch at a time per circuit.</summary>
public sealed class FetchOrchestrator : IDisposable {
    private readonly FetchService _fetch;
    private readonly AppStateService _appState;
    private readonly ILogger<FetchOrchestrator> _logger;
    private CancellationTokenSource? _cts;

    public FetchOrchestrator(FetchService fetch, AppStateService appState, ILogger<FetchOrchestrator> logger) {
        _fetch = fetch;
        _appState = appState;
        _logger = logger;
    }

    public FetchProgress? Progress { get; private set; }
    public AppState? TerminalState { get; private set; }
    public string? FetchingAccountId { get; private set; }

    public bool IsIdle => TerminalState is not null
                           || Progress is null
                           || Progress.State is AppState.AwaitingInput
                               or AppState.Success or AppState.Failed or AppState.Interrupted;

    public int Percent =>
        Progress is { Total: > 0 } p ? (int)Math.Round((double)p.Finished / p.Total * 100) : 0;

    public event Action? Changed;

    public async Task StartFetchAsync(string accountId) {
        TerminalState = null;
        FetchingAccountId = accountId;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        var progress = new Progress<FetchProgress>(p => {
            // Segment events carry no counts, so carry last-known counts forward to avoid clobbering the counter.
            var segmentOnly = p.Segment is not null;
            if (segmentOnly && Progress is not null) {
                Progress = p with {
                    Total = Progress.Total,
                    Finished = Progress.Finished,
                    Failed = Progress.Failed,
                    Retried = Progress.Retried
                };
            } else {
                Progress = p;
            }

            _appState.PipelineState = p.State;
            Changed?.Invoke();
        });

        try {
            var result = await _fetch.FetchPlayerDataAsync(accountId, progress, _cts.Token);
            TerminalState = result;
        } catch (Exception ex) {
            _logger.LogError(ex, "Fetch failed for account {AccountId}", accountId);
            TerminalState = AppState.Failed;
        }

        _appState.PipelineState = TerminalState;
        Changed?.Invoke();
    }

    public void StopFetch() {
        _cts?.Cancel();
    }

    public void Dispose() {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
