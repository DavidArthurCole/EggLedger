using System.Globalization;
using EggLedger.Domain.Api;
using EggLedger.Domain.Ei;
using EggLedger.Domain.MissionPacking;
using EggLedger.Web.Data;
using Ei;

namespace EggLedger.Web.Services;

/// <summary>The EID-fetch pipeline: fetch a player's first-contact backup, diff completed missions against the local IndexedDB store, then fan out bounded-concurrency workers to fetch and store the missing missions. Reports progress and drives the <see cref="AppState"/> machine.</summary>
public sealed class FetchService {
    private const int DefaultWorkerCount = 1;
    private const int MaxWorkerCount = 10;
    private static readonly TimeSpan BackupMinGap = TimeSpan.FromHours(12);

    private readonly ApiClient _api;
    private readonly IndexedDbMissionStore _store;
    private readonly IndexedDbSettings _settings;
    private readonly IApiPayloadDecoder _decoder;
    private readonly MissionPacker _packer;

    public FetchService(ApiClient api, IndexedDbMissionStore store, IndexedDbSettings settings, IApiPayloadDecoder decoder, MissionPacker? packer = null) {
        _api = api;
        _store = store;
        _settings = settings;
        _decoder = decoder;
        _packer = packer ?? new MissionPacker(EiafxMissionConfigSource.Instance);
    }

    /// <summary>Runs the full fetch pipeline, returning the terminal <see cref="AppState"/>. Cancellation drives the pipeline to Interrupted.</summary>
    public async Task<AppState> FetchPlayerDataAsync(
        string playerId,
        IProgress<FetchProgress>? progress,
        CancellationToken cancellationToken) {
        int total = 0;
        int finished = 0;
        int failed = 0;
        int retried = 0;

        void Report(AppState state) =>
            progress?.Report(new FetchProgress {
                State = state,
                Total = Volatile.Read(ref total),
                Finished = Volatile.Read(ref finished),
                Failed = Volatile.Read(ref failed),
                Retried = Volatile.Read(ref retried),
            });

        // First contact.
        Report(AppState.FetchingSave);
        EggIncFirstContactResponse fc;
        try {
            fc = await FetchFirstContactAsync(playerId, cancellationToken).ConfigureAwait(false);
        } catch (OperationCanceledException) {
            Report(AppState.Interrupted);
            return AppState.Interrupted;
        }

        // Diff completed missions against the local store.
        var completed = fc.GetCompletedMissions();
        var existing = await _store.GetCompleteMissionIdsAsync(playerId).ConfigureAwait(false) ?? [];
        var seen = new HashSet<string>(existing, StringComparer.Ordinal);

        var toFetch = new List<(string Id, double Start)>();
        foreach (var mission in completed) {
            if (seen.Add(mission.Identifier)) {
                toFetch.Add((mission.Identifier, mission.StartTimeDerived));
            }
        }
        Volatile.Write(ref total, toFetch.Count);

        if (cancellationToken.IsCancellationRequested) {
            Report(AppState.Interrupted);
            return AppState.Interrupted;
        }

        var settings = await _settings.GetAllSettingsAsync().ConfigureAwait(false);
        bool retryFailed = ReadRetryFailedSetting(settings);
        var failures = new List<FailedMission>();

        if (total > 0) {
            Report(AppState.FetchingMissions);

            int workerCount = ReadWorkerCount(settings);
            bool interrupted = await RunWorkersAsync(
                playerId, toFetch, workerCount, progress,
                () => {
                    Interlocked.Increment(ref finished);
                    Report(AppState.FetchingMissions);
                },
                fm => {
                    Interlocked.Increment(ref failed);
                    if (retryFailed) {
                        lock (failures) {
                            failures.Add(fm);
                        }
                    }
                },
                cancellationToken).ConfigureAwait(false);

            if (interrupted) {
                Report(AppState.Interrupted);
                return AppState.Interrupted;
            }

            // Retry pass over the failed set when the setting is on.
            if (Volatile.Read(ref failed) > 0 && retryFailed && failures.Count > 0) {
                Volatile.Write(ref retried, failures.Count);
                Interlocked.Add(ref total, failures.Count);
                var retrySet = failures.Select(f => (f.MissionId, f.StartTimestamp)).ToList();
                Interlocked.Exchange(ref failed, 0);

                bool retryInterrupted = await RunWorkersAsync(
                    playerId, retrySet, workerCount, progress,
                    () => {
                        Interlocked.Increment(ref finished);
                        Report(AppState.FetchingMissions);
                    },
                    _ => Interlocked.Increment(ref failed),
                    cancellationToken).ConfigureAwait(false);

                if (retryInterrupted) {
                    Report(AppState.Interrupted);
                    return AppState.Interrupted;
                }
            }

            if (Volatile.Read(ref failed) > 0) {
                Report(AppState.Failed);
                return AppState.Failed;
            }
        }

        Report(AppState.ExportingData);
        // Export is DownloadService's job; the fetch service stops at persistence. State reported for parity with the Go state machine.

        Report(AppState.Success);
        return AppState.Success;
    }

    /// <summary>Fetches, validates, and stores the first-contact backup (12h min-gap dedup). Throws "please double check your ID" on validation failure.</summary>
    private async Task<EggIncFirstContactResponse> FetchFirstContactAsync(string playerId, CancellationToken cancellationToken) {
        byte[] payload = await _api.RequestFirstContactRawPayloadAsync(playerId, cancellationToken).ConfigureAwait(false);
        var fc = await _decoder.DecodeFirstContactAsync(payload, cancellationToken).ConfigureAwait(false);
        var invalid = fc.Validate();
        if (invalid is not null) {
            throw new InvalidOperationException(
                $"please double check your ID: error fetching backup for player {playerId}: {invalid.Message}", invalid);
        }

        double lastBackupTime = fc.Backup?.settings?.LastBackupTime ?? 0;
        if (lastBackupTime != 0) {
            // Swallow store errors so a backup write hiccup does not abort the fetch.
            try {
                await _store.InsertBackupAsync(playerId, lastBackupTime, payload, BackupMinGap).ConfigureAwait(false);
            } catch {
                // Intentionally ignored (log-and-continue).
            }
        }
        return fc;
    }

    /// <summary>Fans out bounded-concurrency tasks over the mission set, gated by a SemaphoreSlim. No per-batch sleep: the browser has no shared rate limiter. Returns true if cancellation was observed.</summary>
    private async Task<bool> RunWorkersAsync(
        string playerId,
        IReadOnlyList<(string Id, double Start)> missions,
        int workerCount,
        IProgress<FetchProgress>? progress,
        Action onFinished,
        Action<FailedMission> onError,
        CancellationToken cancellationToken) {
        using var sem = new SemaphoreSlim(workerCount, workerCount);
        var tasks = new List<Task>(missions.Count);

        foreach (var (id, start) in missions) {
            if (cancellationToken.IsCancellationRequested) {
                break;
            }
            await sem.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested) {
                sem.Release();
                break;
            }
            tasks.Add(Task.Run(async () => {
                try {
                    await FetchOneMissionAsync(playerId, id, start, progress, cancellationToken).ConfigureAwait(false);
                } catch (OperationCanceledException) {
                    // Non-success; the outer cancellation check turns the run into Interrupted.
                    onError(new FailedMission(id, start));
                } catch {
                    onError(new FailedMission(id, start));
                } finally {
                    onFinished();
                    sem.Release();
                }
            }, CancellationToken.None));
        }

        try {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        } catch (OperationCanceledException) {
            // Pending Task.Run bodies cancelled before starting; handled below.
        }

        return cancellationToken.IsCancellationRequested;
    }

    /// <summary>Fetches and stores a single mission. Cache hit returns without an API call; otherwise fetch, decode, validate Success + non-empty Artifacts, pack filter columns, and store.</summary>
    private async Task FetchOneMissionAsync(
        string playerId,
        string missionId,
        double startTimestamp,
        IProgress<FetchProgress>? progress,
        CancellationToken cancellationToken) {
        void Track(string segment, SegmentStatus status) =>
            progress?.Report(new FetchProgress {
                State = AppState.FetchingMissions,
                MissionId = missionId,
                Segment = segment,
                SegmentStatus = status,
            });

        Track("Cache", SegmentStatus.Active);
        var cached = await _store.GetCompleteMissionAsync(playerId, missionId).ConfigureAwait(false);
        if (cached is not null) {
            Track("Cache", SegmentStatus.Done);
            return;
        }
        Track("Cache", SegmentStatus.Skipped);

        Track("Fetch", SegmentStatus.Active);
        byte[] payload;
        try {
            payload = await _api.RequestCompleteMissionRawPayloadAsync(playerId, missionId, cancellationToken).ConfigureAwait(false);
        } catch {
            Track("Fetch", SegmentStatus.Failed);
            throw;
        }
        Track("Fetch", SegmentStatus.Done);

        Track("Decode", SegmentStatus.Active);
        CompleteMissionResponse resp;
        try {
            resp = await _decoder.DecodeCompleteMissionAsync(payload, cancellationToken).ConfigureAwait(false);
        } catch {
            Track("Decode", SegmentStatus.Failed);
            throw;
        }
        if (!resp.Success) {
            Track("Decode", SegmentStatus.Failed);
            throw new InvalidOperationException(
                $"error fetching mission {missionId} for player {playerId}: success is false");
        }
        if (resp.Artifacts.Count == 0) {
            Track("Decode", SegmentStatus.Failed);
            throw new InvalidOperationException(
                $"error fetching mission {missionId} for player {playerId}: no artifact found in server response");
        }
        Track("Decode", SegmentStatus.Done);

        Track("Store", SegmentStatus.Active);
        int missionType = resp.Info is not null ? (int)resp.Info.Type : -1;
        _packer.TryComputeMissionFilterCols(startTimestamp, resp, out var cols);
        try {
            await _store.InsertCompleteMissionAsync(
                playerId, missionId, startTimestamp, payload, missionType, cols, resp).ConfigureAwait(false);
        } catch {
            Track("Store", SegmentStatus.Failed);
            throw;
        }
        Track("Store", SegmentStatus.Done);
    }

    private static int ReadWorkerCount(Dictionary<string, string> settings) {
        int n = DefaultWorkerCount;
        if (settings.TryGetValue("worker_count", out var raw)
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) {
            n = parsed;
        }
        return Math.Clamp(n, 1, MaxWorkerCount);
    }

    private static bool ReadRetryFailedSetting(Dictionary<string, string> settings) =>
        settings.TryGetValue("retry_failed_missions", out var raw)
            && bool.TryParse(raw, out var b) && b;
}
