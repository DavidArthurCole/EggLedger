using System.IO.Compression;
using EggLedger.Domain.Api;
using EggLedger.Domain.MissionPacking;
using EggLedger.Domain.MissionQuery;
using Ei;

namespace EggLedger.Web.Data;

/// <summary>
/// IndexedDB-backed <see cref="IMissionStore"/> (C# port of Go missionquery over SQLite).
/// Per-player reads use the <c>player_id</c> index. Stored <c>complete_payload</c> is
/// gzip(raw API response): gunzip, decode as an AuthenticatedMessage, then restore
/// StartTimeDerived from the row's start_timestamp.
/// </summary>
public sealed class IndexedDbMissionStore : IMissionStore {
    private const string MissionStore = "mission";
    private const string BackupStore = "backup";
    private const string ArtifactDropsStore = "artifact_drops";
    private const string PlayerIdIndex = "player_id";

    private readonly IIndexedDb _db;
    private readonly MissionPacker _packer;
    private readonly IApiPayloadDecoder _decoder;
    private readonly IndexedDbAccountStore? _accounts;

    /// <param name="db">IndexedDB wrapper.</param>
    /// <param name="decoder">Payload decoder. Desktop uses in-process protobuf-net; WASM delegates to the sync server (browser cannot emit).</param>
    /// <param name="packer">Mission compiler. When null, built from the canonical eiafx config via <see cref="EiafxMissionConfigSource.Instance"/>.</param>
    /// <param name="accounts">Known-account store. When null, <see cref="GetKnownAccountsAsync"/> returns empty (mission/backup schema has no account rows).</param>
    public IndexedDbMissionStore(IIndexedDb db, IApiPayloadDecoder decoder, MissionPacker? packer = null, IndexedDbAccountStore? accounts = null) {
        _db = db;
        _decoder = decoder;
        _packer = packer ?? new MissionPacker(EiafxMissionConfigSource.Instance);
        _accounts = accounts;
    }

    /// <summary>Mission ids for the player, ordered by start_timestamp.</summary>
    public async Task<IReadOnlyList<string>?> GetCompleteMissionIdsAsync(string playerId) {
        var rows = await PlayerRowsAsync(playerId);
        return rows.OrderBy(r => r.StartTimestamp)
            .Select(r => r.MissionId)
            .ToList();
    }

    /// <summary>Known accounts from the injected <see cref="IndexedDbAccountStore"/>; empty when no account store is wired.</summary>
    public async Task<IReadOnlyList<KnownAccount>> GetKnownAccountsAsync() {
        if (_accounts is null) {
            return [];
        }
        var accounts = await _accounts.GetKnownAccountsAsync().ConfigureAwait(false);
        return accounts.Select(a => a.ToKnownAccount()).ToList();
    }

    /// <summary>Mission count and max return_timestamp for the player. Mirrors Go RetrievePlayerMissionStats.</summary>
    public async Task<PlayerMissionStats?> GetPlayerMissionStatsAsync(string playerId) {
        var rows = await PlayerRowsAsync(playerId);
        double max = rows.Count == 0 ? 0 : rows.Max(r => r.ReturnTimestamp);
        return new PlayerMissionStats(rows.Count, max);
    }

    /// <summary>Streams every decoded mission for the player, ordered by start_timestamp. Returns false on a decode error (mirrors Go).</summary>
    public async Task<bool> StreamPlayerCompleteMissionsAsync(string playerId, Action<CompleteMissionResponse> onMission) {
        var rows = await PlayerRowsAsync(playerId);
        foreach (var row in rows.OrderBy(r => r.StartTimestamp)) {
            CompleteMissionResponse cm;
            // Only a decode failure is a store error; let consumer-callback bugs surface.
            try {
                cm = await DecodeAsync(row).ConfigureAwait(false);
            } catch {
                return false;
            }
            onMission(cm);
        }
        return true;
    }

    /// <summary>One decoded mission for (player, mission). Null on cache miss or decode error (Go returns nil for both).</summary>
    public async Task<CompleteMissionResponse?> GetCompleteMissionAsync(string playerId, string missionId) {
        var rows = await PlayerRowsAsync(playerId);
        var row = rows.FirstOrDefault(r => r.MissionId == missionId);
        if (row is null) {
            return null;
        }
        try {
            return await DecodeAsync(row).ConfigureAwait(false);
        } catch {
            return null;
        }
    }

    /// <summary>Count of player missions missing migration-6 filter columns (ship == -1). Mirrors Go CountPendingFilterCols.</summary>
    public async Task<int?> CountPendingFilterColsAsync(string eid) {
        var rows = await PlayerRowsAsync(eid);
        return rows.Count(r => r.Ship == -1);
    }

    /// <summary>Fast path: display rows from stored filter columns only (ship != -1), ordered by start_timestamp.</summary>
    public async Task<IReadOnlyList<IMissionRow>?> GetPlayerMissionMetaAsync(string eid) {
        var rows = await PlayerRowsAsync(eid);
        var result = new List<IMissionRow>();
        foreach (var row in rows.Where(r => r.Ship != -1).OrderBy(r => r.StartTimestamp)) {
            result.Add(_packer.MissionMetaToDBMission(ToMeta(row)));
        }
        return result;
    }

    /// <summary>Slow path: every decoded mission for the player, ordered by start_timestamp. Null on a decode error.</summary>
    public async Task<IReadOnlyList<CompleteMissionResponse>?> GetPlayerCompleteMissionsAsync(string eid) {
        var rows = (await PlayerRowsAsync(eid)).OrderBy(r => r.StartTimestamp).ToList();
        try {
            // Decode is a server round-trip per mission in WASM; sequential over a full
            // history was ~20s. Run in bounded-concurrency batches, preserving order.
            var result = new CompleteMissionResponse[rows.Count];
            const int batch = 16;
            for (int start = 0; start < rows.Count; start += batch) {
                int end = Math.Min(start + batch, rows.Count);
                var tasks = new Task[end - start];
                for (int i = start; i < end; i++) {
                    int idx = i;
                    tasks[idx - start] = Assign(idx);
                }
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            return result;

            async Task Assign(int idx) => result[idx] = await DecodeAsync(rows[idx]).ConfigureAwait(false);
        } catch {
            return null;
        }
    }

    /// <summary>Compiles a decoded mission into a display row (slow path).</summary>
    public IMissionRow CompileMissionInformation(CompleteMissionResponse mission) =>
        _packer.CompileMissionInformation(mission);

    private readonly HashSet<string> _backfilling = [];

    // Decode each column-less mission once, compute + persist its filter columns,
    // so subsequent views use the no-decode fast path. Without this every Mission
    // Data view re-decodes the whole history (one server round-trip per mission).
    public void QueueFilterColBackfill(string eid) {
        if (_backfilling.Add(eid))
            _ = BackfillFilterColsAsync(eid);
    }

    private async Task BackfillFilterColsAsync(string eid) {
        try {
            foreach (var row in await PlayerRowsAsync(eid).ConfigureAwait(false)) {
                if (row.Ship != -1)
                    continue;

                CompleteMissionResponse decoded;
                try { decoded = await DecodeAsync(row).ConfigureAwait(false); } catch { continue; }

                if (_packer.TryComputeMissionFilterCols(row.StartTimestamp, decoded, out var cols))
                    await _db.PutAsync(MissionStore, WithCols(row, cols)).ConfigureAwait(false);
            }
        } finally {
            _backfilling.Remove(eid);
        }
    }

    private static MissionRow WithCols(MissionRow row, MissionFilterCols cols) => row with {
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

    /// <summary>
    /// Inserts the first-contact backup (Go db.InsertBackup): raw payload gzipped.
    /// When <paramref name="minimumGap"/> is positive and the existing backup is
    /// newer than that gap, the write is skipped (the 12h dedup). The browser
    /// <c>backup</c> store keys on <c>player_id</c> alone, so there is one row per player.
    /// </summary>
    public async Task InsertBackupAsync(string playerId, double timestamp, byte[] rawPayload, TimeSpan minimumGap) {
        if (minimumGap > TimeSpan.Zero) {
            var existing = await _db.GetAsync<BackupRow>(BackupStore, playerId);
            if (existing is not null) {
                double gapSeconds = timestamp - existing.RecordedAt;
                if (gapSeconds < minimumGap.TotalSeconds) {
                    return;
                }
            }
        }

        var row = new BackupRow {
            PlayerId = playerId,
            RecordedAt = timestamp,
            Payload = Gzip(rawPayload),
        };
        await _db.PutAsync(BackupStore, row);
    }

    /// <summary>
    /// Inserts one fetched mission plus its artifact_drops rows. <paramref name="rawPayload"/>
    /// is the raw AuthenticatedMessage wire, gzipped into <c>complete_payload</c> exactly as
    /// <see cref="GetCompleteMissionAsync"/> reads it back. Filter columns precomputed by the caller.
    /// </summary>
    public async Task InsertCompleteMissionAsync(
        string playerId,
        string missionId,
        double startTimestamp,
        byte[] rawPayload,
        int missionType,
        MissionFilterCols cols,
        CompleteMissionResponse decoded) {
        var row = new MissionRow {
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
        if (drops.Count > 0) {
            var rows = drops.Select(d => (object)new ArtifactDropRow {
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

    /// <summary>Gzips bytes (Go db.compress); inverse of <see cref="Gunzip"/>.</summary>
    private static byte[] Gzip(byte[] data) {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true)) {
            gzip.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    private async Task<List<MissionRow>> PlayerRowsAsync(string playerId) {
        var rows = await _db.GetAllByIndexAsync<MissionRow>(MissionStore, PlayerIdIndex, playerId);
        return [.. rows];
    }

    public async Task<IReadOnlyList<StoredDrop>?> GetStoredPlayerDropsAsync(string playerId) {
        try {
            var rows = await _db.GetAllByIndexAsync<ArtifactDropRow>(ArtifactDropsStore, PlayerIdIndex, playerId);
            return [.. rows.Select(r => new StoredDrop(r.MissionId, r.ArtifactId, r.Level, r.Rarity))];
        } catch {
            return null;
        }
    }

    /// <summary>
    /// Gunzips the stored payload, decodes it as an AuthenticatedMessage wrapping a
    /// CompleteMissionResponse, then restores StartTimeDerived from the row's start_timestamp.
    /// </summary>
    private async Task<CompleteMissionResponse> DecodeAsync(MissionRow row) {
        byte[] raw = Gunzip(row.CompletePayload);
        var resp = await _decoder.DecodeCompleteMissionAsync(raw).ConfigureAwait(false);
        resp.Info?.StartTimeDerived = row.StartTimestamp;
        return resp;
    }

    private static byte[] Gunzip(byte[] data) {
        using var input = new MemoryStream(data, writable: false);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    private static MissionMeta ToMeta(MissionRow row) => new() {
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
