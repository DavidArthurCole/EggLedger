namespace EggLedger.Web.Services;

/// <summary>Pipeline lifecycle states reached by the browser fetch pipeline. The desktop-only RESOLVING_MISSION_TYPES state is absent: the browser store backfills filter columns lazily on read.</summary>
public enum AppState {
    AwaitingInput,
    FetchingSave,
    FetchingMissions,
    ExportingData,
    Success,
    Failed,
    Interrupted,
}

/// <summary>Per-mission segment status fed to the segmented progress UI. The four segments are Cache, Fetch, Decode, Store.</summary>
public enum SegmentStatus {
    Active,
    Done,
    Failed,
    Skipped,
}

/// <summary>One progress event: an overall state transition, a mission counter snapshot, or a per-mission segment update, collapsed into a single <see cref="System.IProgress{T}"/> channel.</summary>
public sealed record FetchProgress {
    public required AppState State { get; init; }

    public int Total { get; init; }

    /// <summary>Finished = cache hits + successful fetches.</summary>
    public int Finished { get; init; }

    public int Failed { get; init; }

    /// <summary>Missions re-attempted in the retry pass.</summary>
    public int Retried { get; init; }

    /// <summary>Mission id this segment update applies to, or null for state/counter-only events.</summary>
    public string? MissionId { get; init; }

    /// <summary>Segment name (Cache/Fetch/Decode/Store), or null.</summary>
    public string? Segment { get; init; }

    /// <summary>Set only when <see cref="Segment"/> is set.</summary>
    public SegmentStatus? SegmentStatus { get; init; }
}

/// <summary>A mission that failed to fetch, captured for the retry pass.</summary>
public sealed record FailedMission(string MissionId, double StartTimestamp);
