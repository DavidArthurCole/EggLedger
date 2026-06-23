using Ei;

namespace EggLedger.Domain.MissionQuery;

/// <summary>
/// Data-access contract the MissionQuery handlers run against. Mirrors the
/// `db` and `storage` calls the Go missionquery package makes via its injected
/// `*sql.DB` + `*storage.AppStorage`. The real SQLite implementation is
/// deferred to a later phase; tests supply an in-memory double.
///
/// Methods map 1:1 to Go calls (async because IndexedDB access is async):
///  - GetCompleteMissionIdsAsync       -> db.RetrievePlayerCompleteMissionIds
///  - GetKnownAccountsAsync            -> storage.AppStorage.KnownAccounts (copied)
///  - GetPlayerMissionStatsAsync       -> db.RetrievePlayerMissionStats
///  - StreamPlayerCompleteMissionsAsync -> db.RetrievePlayerCompleteMissionsStream
///  - GetCompleteMissionAsync          -> db.RetrieveCompleteMission
///  - CountPendingFilterColsAsync      -> db.CountPendingFilterCols
///  - GetPlayerMissionMetaAsync        -> db.RetrievePlayerMissionMeta (fast path)
///  - GetPlayerCompleteMissionsAsync   -> db.RetrievePlayerCompleteMissions (slow path)
///  - QueueFilterColBackfill           -> background db.ResolvePendingFilterCols
/// </summary>
public interface IMissionStore
{
    /// <summary>Complete mission identifiers stored for the player. Null on error.</summary>
    Task<IReadOnlyList<string>?> GetCompleteMissionIdsAsync(string playerId);

    /// <summary>Accounts known to local storage, in order. Snapshot copy.</summary>
    Task<IReadOnlyList<KnownAccount>> GetKnownAccountsAsync();

    /// <summary>
    /// Mission count and most-recent return timestamp for the player. Returns
    /// null on a data-access error so the caller can skip the account (Go logs
    /// and continues).
    /// </summary>
    Task<PlayerMissionStats?> GetPlayerMissionStatsAsync(string playerId);

    /// <summary>
    /// Streams every stored complete mission for the player, invoking
    /// <paramref name="onMission"/> per mission. Returns false on error (Go
    /// returns the error and the caller yields nil). Mirrors the streaming call
    /// used to bound transient allocation.
    /// </summary>
    Task<bool> StreamPlayerCompleteMissionsAsync(string playerId, Action<CompleteMissionResponse> onMission);

    /// <summary>
    /// One complete mission for (player, mission). Null = cache miss (Go returns
    /// nil mission, no error). Returns null on error too; the handler treats
    /// both as "no drops".
    /// </summary>
    Task<CompleteMissionResponse?> GetCompleteMissionAsync(string playerId, string missionId);

    /// <summary>
    /// Count of stored missions for the EID still missing migration-6 filter
    /// columns. Null signals the count itself errored (Go's countErr != nil),
    /// which forces the slow path and suppresses the backfill kick.
    /// </summary>
    Task<int?> CountPendingFilterColsAsync(string eid);

    /// <summary>
    /// Fast path: pre-shaped missions built from DB filter columns only (no
    /// payload decode). Null on error.
    /// </summary>
    Task<IReadOnlyList<IMissionRow>?> GetPlayerMissionMetaAsync(string eid);

    /// <summary>
    /// Slow path: full complete-mission payloads to decode + compile. Null on
    /// error.
    /// </summary>
    Task<IReadOnlyList<CompleteMissionResponse>?> GetPlayerCompleteMissionsAsync(string eid);

    /// <summary>
    /// Compiles a decoded complete mission into a display row (slow path).
    /// Mirrors missionpacking.CompileMissionInformation; injected so this
    /// package need not hard-depend on the concurrently ported MissionPacking.
    /// Pure in-memory shaping, hence synchronous.
    /// </summary>
    IMissionRow CompileMissionInformation(CompleteMissionResponse mission);

    /// <summary>
    /// Kicks a one-time background filter-column backfill for the EID. Called
    /// only when CountPendingFilterCols succeeded and returned &gt; 0.
    /// </summary>
    void QueueFilterColBackfill(string eid);
}

/// <summary>
/// Opaque display row returned by mission listing. The concrete type lives in
/// MissionPacking (DatabaseMission); this package only passes it through, so it
/// is kept as a marker to avoid a cross-package hard dependency for A8.
/// </summary>
public interface IMissionRow
{
}
