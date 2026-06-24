using Ei;

namespace EggLedger.Domain.MissionQuery;

/// <summary>
/// Data-access contract for the MissionQuery handlers. Mirrors the Go missionquery
/// db/storage calls; methods async because IndexedDB access is async.
/// </summary>
public interface IMissionStore {
    /// <summary>Complete mission identifiers stored for the player. Null on error.</summary>
    Task<IReadOnlyList<string>?> GetCompleteMissionIdsAsync(string playerId);

    /// <summary>Accounts known to local storage, in order. Snapshot copy.</summary>
    Task<IReadOnlyList<KnownAccount>> GetKnownAccountsAsync();

    /// <summary>
    /// Mission count and most-recent return timestamp for the player. Null on
    /// data-access error so the caller can skip the account.
    /// </summary>
    Task<PlayerMissionStats?> GetPlayerMissionStatsAsync(string playerId);

    /// <summary>
    /// Streams every stored complete mission for the player, invoking
    /// <paramref name="onMission"/> per mission. False on error.
    /// </summary>
    Task<bool> StreamPlayerCompleteMissionsAsync(string playerId, Action<CompleteMissionResponse> onMission);

    /// <summary>
    /// One complete mission for (player, mission). Null on cache miss or error;
    /// the handler treats both as "no drops".
    /// </summary>
    Task<CompleteMissionResponse?> GetCompleteMissionAsync(string playerId, string missionId);

    /// <summary>
    /// Count of stored missions for the EID still missing migration-6 filter columns.
    /// Null means the count errored, which forces the slow path and suppresses backfill.
    /// </summary>
    Task<int?> CountPendingFilterColsAsync(string eid);

    /// <summary>Fast path: missions built from DB filter columns only, no payload decode. Null on error.</summary>
    Task<IReadOnlyList<IMissionRow>?> GetPlayerMissionMetaAsync(string eid);

    /// <summary>Slow path: full complete-mission payloads to decode + compile. Null on error.</summary>
    Task<IReadOnlyList<CompleteMissionResponse>?> GetPlayerCompleteMissionsAsync(string eid);

    /// <summary>
    /// Stored artifact-drop rows for the player, read directly from the drops store
    /// (no per-mission decode). Used by the Lifetime tab so it does not re-decode the
    /// entire mission history (in the browser each decode is a server round-trip).
    /// Null on error.
    /// </summary>
    Task<IReadOnlyList<StoredDrop>?> GetStoredPlayerDropsAsync(string playerId);

    /// <summary>
    /// Compiles a decoded complete mission into a display row (slow path).
    /// Mirrors missionpacking.CompileMissionInformation; injected to avoid a hard dependency.
    /// </summary>
    IMissionRow CompileMissionInformation(CompleteMissionResponse mission);

    /// <summary>
    /// Kicks a one-time background filter-column backfill for the EID. Called only
    /// when CountPendingFilterCols succeeded and returned &gt; 0.
    /// </summary>
    void QueueFilterColBackfill(string eid);
}

/// <summary>A stored artifact drop: which mission, and the spec identity needed to rebuild it.</summary>
public sealed record StoredDrop(string MissionId, int ArtifactId, int Level, int Rarity);

/// <summary>
/// Opaque display row returned by mission listing. Concrete type is MissionPacking.DatabaseMission;
/// kept as a marker here to avoid a cross-package hard dependency.
/// </summary>
public interface IMissionRow {
}
