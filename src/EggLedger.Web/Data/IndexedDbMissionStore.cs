using System.IO.Compression;
using Ei;
using EggLedger.Domain.Api;
using EggLedger.Domain.MissionPacking;
using EggLedger.Domain.MissionQuery;

namespace EggLedger.Web.Data;

/// <summary>
/// IndexedDB-backed <see cref="IMissionStore"/>. C# port of the data access the
/// Go missionquery package performs against SQLite, reading the browser
/// IndexedDB <c>mission</c> store instead. Per-player reads use the
/// <c>player_id</c> index. Stored <c>complete_payload</c> bytes are gzip(raw API
/// response): gunzip, then decode as an AuthenticatedMessage exactly like Go
/// api.DecodeCompleteMissionPayload, then restore StartTimeDerived from the
/// row's start_timestamp.
/// </summary>
public sealed class IndexedDbMissionStore : IMissionStore
{
    private const string MissionStore = "mission";
    private const string BackupStore = "backup";
    private const string ArtifactDropsStore = "artifact_drops";
    private const string PlayerIdIndex = "player_id";

    // Matches the Go decode endpoint label; only affects error text.
    private const string CompleteMissionUrl =
        ApiClient.DefaultApiPrefix + "/ei_afx/complete_mission";

    private readonly IIndexedDb _db;
    private readonly MissionPacker _packer;
    private readonly ApiClient _api;
    private readonly IndexedDbAccountStore? _accounts;

    /// <param name="db">IndexedDB wrapper.</param>
    /// <param name="packer">
    /// Mission compiler. When null, one is built from the canonical eiafx config
    /// via <see cref="EiafxMissionConfigSource.Instance"/>.
    /// </param>
    /// <param name="api">
    /// Payload decoder. When null, a default <see cref="ApiClient"/> is used;
    /// only its decode logic is exercised, never the network.
    /// </param>
    /// <param name="accounts">
    /// Known-account store. When null, <see cref="GetKnownAccountsAsync"/> returns
    /// an empty list (the mission/backup schema carries no account rows). Supplied
    /// in the app so GetExistingData joins the persisted accounts with stats.
    /// </param>
    public IndexedDbMissionStore(IIndexedDb db, MissionPacker? packer = null, ApiClient? api = null, IndexedDbAccountStore? accounts = null)
    {
        _db = db;
        _packer = packer ?? new MissionPacker(EiafxMissionConfigSource.Instance);
        _api = api ?? new ApiClient();
        _accounts = accounts;
    }

    /// <summary>Mission ids for the player, ordered by start_timestamp.</summary>
    public async Task<IReadOnlyList<string>?> GetCompleteMissionIdsAsync(string playerId)
    {
        var rows = await PlayerRowsAsync(playerId);
        return rows.OrderBy(r => r.StartTimestamp)
            .Select(r => r.MissionId)
            .ToList();
    }

    /// <summary>
    /// Known accounts. The mission/backup schema carries no account rows, so the
    /// list comes from the injected <see cref="IndexedDbAccountStore"/> (the Go
    /// storage known-accounts list, persisted in the settings store). Returns an
    /// empty list when no account store is wired.
    /// </summary>
    public async Task<IReadOnlyList<KnownAccount>> GetKnownAccountsAsync()
    {
        if (_accounts is null)
        {
            return [];
        }
        var accounts = await _accounts.GetKnownAccountsAsync().ConfigureAwait(false);
        return accounts.Select(a => a.ToKnownAccount()).ToList();
    }

    /// <summary>
    /// Mission count and max return_timestamp for the player. Mirrors Go
    /// RetrievePlayerMissionStats (COUNT(*), COALESCE(MAX(return_timestamp), 0)).
    /// </summary>
    public async Task<PlayerMissionStats?> GetPlayerMissionStatsAsync(string playerId)
    {
        var rows = await PlayerRowsAsync(playerId);
        double max = rows.Count == 0 ? 0 : rows.Max(r => r.ReturnTimestamp);
        return new PlayerMissionStats(rows.Count, max);
    }

    /// <summary>
    /// Streams every decoded complete mission for the player, ordered by
    /// start_timestamp. Returns false on a decode error to mirror Go's
    /// error-then-nil behavior.
    /// </summary>
    public async Task<bool> StreamPlayerCompleteMissionsAsync(string playerId, Action<CompleteMissionResponse> onMission)
    {
        var rows = await PlayerRowsAsync(playerId);
        foreach (var row in rows.OrderBy(r => r.StartTimestamp))
        {
            CompleteMissionResponse cm;
            // Only a decode failure is a store error; let consumer-callback bugs surface.
            try
            {
                cm = Decode(row);
            }
            catch
            {
                return false;
            }
            onMission(cm);
        }
        return true;
    }

    /// <summary>
    /// One decoded mission for (player, mission). Null on cache miss or decode
    /// error (Go returns nil mission for both).
    /// </summary>
    public async Task<CompleteMissionResponse?> GetCompleteMissionAsync(string playerId, string missionId)
    {
        var rows = await PlayerRowsAsync(playerId);
        var row = rows.FirstOrDefault(r => r.MissionId == missionId);
        if (row is null)
        {
            return null;
        }
        try
        {
            return Decode(row);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Count of player missions still missing migration-6 filter columns
    /// (ship == -1). Mirrors Go CountPendingFilterCols. Never errors here, so
    /// never returns null.
    /// </summary>
    public async Task<int?> CountPendingFilterColsAsync(string eid)
    {
        var rows = await PlayerRowsAsync(eid);
        return rows.Count(r => r.Ship == -1);
    }

    /// <summary>
    /// Fast path: display rows built from stored filter columns only (ship != -1),
    /// ordered by start_timestamp. Mirrors Go RetrievePlayerMissionMeta +
    /// MissionMetaToDBMission.
    /// </summary>
    public async Task<IReadOnlyList<IMissionRow>?> GetPlayerMissionMetaAsync(string eid)
    {
        var rows = await PlayerRowsAsync(eid);
        var result = new List<IMissionRow>();
        foreach (var row in rows.Where(r => r.Ship != -1).OrderBy(r => r.StartTimestamp))
        {
            result.Add(_packer.MissionMetaToDBMission(ToMeta(row)));
        }
        return result;
    }

    /// <summary>
    /// Slow path: every decoded complete mission for the player, ordered by
    /// start_timestamp. Null on a decode error.
    /// </summary>
    public async Task<IReadOnlyList<CompleteMissionResponse>?> GetPlayerCompleteMissionsAsync(string eid)
    {
        var rows = await PlayerRowsAsync(eid);
        var result = new List<CompleteMissionResponse>(rows.Count);
        try
        {
            foreach (var row in rows.OrderBy(r => r.StartTimestamp))
            {
                result.Add(Decode(row));
            }
        }
        catch
        {
            return null;
        }
        return result;
    }

    /// <summary>Compiles a decoded mission into a display row (slow path).</summary>
    public IMissionRow CompileMissionInformation(CompleteMissionResponse mission) =>
        _packer.CompileMissionInformation(mission);

    /// <summary>
    /// No background backfill runs in the browser yet. The fast path rebuilds
    /// from stored columns on the next view; this is a no-op until a backfill
    /// worker is ported.
    /// </summary>
    public void QueueFilterColBackfill(string eid)
    {
    }

    /// <summary>
    /// Inserts the first-contact backup. Mirrors Go db.InsertBackup: the raw
    /// payload is gzipped and stored with <c>recorded_at = timestamp</c>. When
    /// <paramref name="minimumGap"/> is positive and the player's existing backup
    /// is newer than that gap, the write is skipped (the 12h dedup). The browser
    /// <c>backup</c> store keys on <c>player_id</c> alone, so there is at most one
    /// row per player; the dedup compares against that single row.
    /// </summary>
    public async Task InsertBackupAsync(string playerId, double timestamp, byte[] rawPayload, TimeSpan minimumGap)
    {
        if (minimumGap > TimeSpan.Zero)
        {
            var existing = await _db.GetAsync<BackupRow>(BackupStore, playerId);
            if (existing is not null)
            {
                double gapSeconds = timestamp - existing.RecordedAt;
                if (gapSeconds < minimumGap.TotalSeconds)
                {
                    return;
                }
            }
        }

        var row = new BackupRow
        {
            PlayerId = playerId,
            RecordedAt = timestamp,
            Payload = Gzip(rawPayload),
        };
        await _db.PutAsync(BackupStore, row);
    }

    /// <summary>
    /// Inserts one fetched mission plus its artifact_drops rows. Mirrors Go
    /// db.InsertCompleteMission + db.InsertArtifactDrops: <paramref name="rawPayload"/>
    /// is the raw API payload (the AuthenticatedMessage wire), gzipped into
    /// <c>complete_payload</c> exactly as <see cref="GetCompleteMissionAsync"/>
    /// expects to read it back. Filter columns and mission_type are precomputed by
    /// the caller via the packer.
    /// </summary>
    public async Task InsertCompleteMissionAsync(
        string playerId,
        string missionId,
        double startTimestamp,
        byte[] rawPayload,
        int missionType,
        MissionFilterCols cols,
        CompleteMissionResponse decoded)
    {
        var row = new MissionRow
        {
            PlayerId = playerId,
            MissionId = missionId,
            StartTimestamp = startTimestamp,
            CompletePayload = Gzip(rawPayload),
            MissionType = missionType,
            Ship = cols.Ship,
            DurationType = cols.DurationType,
            Level = cols.Level,
            Capacity = cols.Capacity,
            NominalCapacity = cols.NominalCapacity,
            IsDubCap = cols.IsDubCap,
            IsBuggedCap = cols.IsBuggedCap,
            Target = cols.Target,
            ReturnTimestamp = cols.ReturnTimestamp,
        };
        await _db.PutAsync(MissionStore, row);

        var drops = ArtifactDrops.Build(decoded);
        if (drops.Count > 0)
        {
            var rows = drops.Select(d => (object)new ArtifactDropRow
            {
                MissionId = missionId,
                PlayerId = playerId,
                DropIndex = d.DropIndex,
                ArtifactId = d.ArtifactId,
                SpecType = d.SpecType,
                Level = d.Level,
                Rarity = d.Rarity,
                Quality = d.Quality,
            });
            await _db.PutManyAsync(ArtifactDropsStore, rows);
        }
    }

    /// <summary>
    /// Gzips bytes at default compression. C# port of Go db.compress; the inverse
    /// of <see cref="Gunzip"/> used on the read path.
    /// </summary>
    private static byte[] Gzip(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
        {
            gzip.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    private async Task<List<MissionRow>> PlayerRowsAsync(string playerId)
    {
        var rows = await _db.GetAllByIndexAsync<MissionRow>(MissionStore, PlayerIdIndex, playerId);
        return rows.ToList();
    }

    /// <summary>
    /// Gunzips the stored payload, decodes it as an AuthenticatedMessage wrapping
    /// a CompleteMissionResponse (Go DecodeCompleteMissionPayload), then restores
    /// StartTimeDerived from the row's start_timestamp like the Go DB reads do.
    /// </summary>
    private CompleteMissionResponse Decode(MissionRow row)
    {
        byte[] raw = Gunzip(row.CompletePayload);
        var resp = _api.DecodeApiResponse<CompleteMissionResponse>(CompleteMissionUrl, raw, authenticated: true);
        if (resp.Info is not null)
        {
            resp.Info.StartTimeDerived = row.StartTimestamp;
        }
        return resp;
    }

    private static byte[] Gunzip(byte[] data)
    {
        using var input = new MemoryStream(data, writable: false);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    private static MissionMeta ToMeta(MissionRow row) => new()
    {
        MissionId = row.MissionId,
        StartTimestamp = row.StartTimestamp,
        ReturnTimestamp = row.ReturnTimestamp,
        Ship = row.Ship,
        DurationType = row.DurationType,
        Level = row.Level,
        Capacity = row.Capacity,
        NominalCapacity = row.NominalCapacity,
        IsDubCap = row.IsDubCap,
        IsBuggedCap = row.IsBuggedCap,
        Target = row.Target,
        MissionType = row.MissionType,
    };
}
