namespace EggLedger.Web.Services;

/// <summary>
/// Pipeline lifecycle states. C# port of Go AppState (fetch.go), restricted to
/// the states the browser fetch pipeline reaches. The desktop-only
/// RESOLVING_MISSION_TYPES state is not reached here: the browser store backfills
/// filter columns lazily on read, so no one-time resolution pass runs.
/// </summary>
public enum AppState
{
    AwaitingInput,
    FetchingSave,
    FetchingMissions,
    ExportingData,
    Success,
    Failed,
    Interrupted,
}

/// <summary>
/// Per-mission segment status. Mirrors the Go <c>tracker(name, status)</c>
/// strings ("active", "done", "failed", "skipped") fed to the segmented progress
/// UI. The four segments are Cache, Fetch, Decode, Store.
/// </summary>
public enum SegmentStatus
{
    Active,
    Done,
    Failed,
    Skipped,
}

/// <summary>
/// One progress event. Either an overall <see cref="State"/> transition, an
/// updated mission counter snapshot (<see cref="Total"/>/<see cref="Finished"/>/
/// <see cref="Failed"/>/<see cref="Retried"/>), or a per-mission segment update
/// (<see cref="MissionId"/>, <see cref="Segment"/>, <see cref="SegmentStatus"/>).
/// Mirrors the Go pipeline's combined updateState + reportProgress + tracker
/// reporting, collapsed into a single channel for <see cref="System.IProgress{T}"/>.
/// </summary>
public sealed record FetchProgress
{
    /// <summary>Current pipeline state. Always set.</summary>
    public required AppState State { get; init; }

    /// <summary>Total missions to fetch this run.</summary>
    public int Total { get; init; }

    /// <summary>Missions finished (cache hits + successful fetches).</summary>
    public int Finished { get; init; }

    /// <summary>Missions that failed to fetch.</summary>
    public int Failed { get; init; }

    /// <summary>Missions re-attempted in the retry pass.</summary>
    public int Retried { get; init; }

    /// <summary>
    /// Mission id this event's segment update applies to, or null for
    /// state/counter-only events.
    /// </summary>
    public string? MissionId { get; init; }

    /// <summary>Segment name (Cache/Fetch/Decode/Store), or null.</summary>
    public string? Segment { get; init; }

    /// <summary>Segment status, set only when <see cref="Segment"/> is set.</summary>
    public SegmentStatus? SegmentStatus { get; init; }
}

/// <summary>
/// A mission that failed to fetch, captured for the retry pass. C# port of Go
/// FailedMission (fetch.go).
/// </summary>
public sealed record FailedMission(string MissionId, double StartTimestamp);
