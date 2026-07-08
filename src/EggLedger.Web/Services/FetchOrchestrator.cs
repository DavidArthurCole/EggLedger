using EggLedger.Web.Data;
using EggLedger.Web.State;
using Microsoft.Extensions.Logging;

namespace EggLedger.Web.Services;

/// <summary>Owns fetch execution and live progress at circuit scope, so a fetch survives navigating away from whatever page started it. Single fetch at a time per circuit.</summary>
public sealed class FetchOrchestrator : IDisposable {
    private const string InProgressKeyPrefix = "fetch_in_progress:";

    private readonly FetchService _fetch;
    private readonly AppStateService _appState;
    private readonly IndexedDbSettings _settings;
    private readonly ILogger<FetchOrchestrator> _logger;
    private CancellationTokenSource? _cts;

    public FetchOrchestrator(FetchService fetch, AppStateService appState, IndexedDbSettings settings, ILogger<FetchOrchestrator> logger) {
        _fetch = fetch;
        _appState = appState;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>Account ids whose last fetch never reached a terminal state (interrupted by a circuit teardown, e.g. a page refresh mid-fetch). Callers resume these on next load.</summary>
    public static async Task<List<string>> GetIncompleteAccountsAsync(IndexedDbSettings settings) {
        var all = await settings.GetAllSettingsAsync().ConfigureAwait(false);
        return [.. all.Keys.Where(k => k.StartsWith(InProgressKeyPrefix, StringComparison.Ordinal))
            .Select(k => k[InProgressKeyPrefix.Length..])];
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

        // Cleared only on a clean terminal state below; a circuit teardown mid-fetch (e.g. a
        // page refresh) leaves this set, so GetIncompleteAccountsAsync picks it up next load.
        await _settings.SetSettingAsync(InProgressKeyPrefix + accountId, "1").ConfigureAwait(false);

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

        // Reaching here at all (success, failure, or user-cancelled) means the pipeline ran to
        // completion in this circuit; only a circuit teardown mid-fetch skips this line, which is
        // the exact "never resumed" case GetIncompleteAccountsAsync exists to catch.
        await _settings.RemoveSettingAsync(InProgressKeyPrefix + accountId).ConfigureAwait(false);

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
