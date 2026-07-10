using System.IO.Compression;
using EggLedger.Domain.Api;
using EggLedger.Domain.MissionPacking;
using EggLedger.Domain.MissionQuery;
using Ei;

namespace EggLedger.Web.Data;

/// <summary>
/// IndexedDB-backed <see cref="IMissionStore"/> (Go missionquery over SQLite). Stored
/// <c>complete_payload</c> is gzip(raw API response); see <see cref="DecodeAsync"/> for read-back.
/// </summary>
public sealed class IndexedDbMissionStore : IMissionStore {
    private readonly IIndexedDb _db;
    private readonly MissionPacker _packer;
    private readonly IApiPayloadDecoder _decoder;
    private readonly IndexedDbAccountStore? _accounts;

    /// <param name="db">IndexedDB wrapper.</param>
    /// <param name="decoder">Payload decoder (in-process protobuf-net).</param>
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
        var rows = await PlayerMetaRowsAsync(playerId);
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
        var rows = await PlayerMetaRowsAsync(playerId);
        double max = rows.Count == 0 ? 0 : rows.Max(r => r.ReturnTimestamp);
        return new PlayerMissionStats(rows.Count, max);
    }

    /// <summary>Streams every decoded mission for the player, ordered by start_timestamp. Returns false on a decode error (mirrors Go).</summary>
    public async Task<bool> StreamPlayerCompleteMissionsAsync(string playerId, Action<CompleteMissionResponse> onMission) {
        var rows = await PlayerRowsAsync(playerId);
        foreach (var row in rows.OrderBy(r => r.StartTimestamp)) {
            CompleteMissionResponse cm;
            // Only decode failure is a store error; consumer-callback bugs must surface.
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
        // Indexed single-row lookup on the [player_id, mission_id] composite key, not a full-table
        // scan: the overlay + prev/next navigation hit this per click.
        var key = DecodeKey(playerId, missionId);
        if (DecodeCacheGet(key) is { } hit) {
            return hit;
        }
        var row = await _db.GetAsync<MissionRow>(IndexedDbStores.Mission, new object[] { playerId, missionId }).ConfigureAwait(false);
        if (row is null) {
            return null;
        }
        try {
            var decoded = await DecodeAsync(row).ConfigureAwait(false);
            DecodeCachePut(key, decoded);
            return decoded;
        } catch {
            return null;
        }
    }

    /// <summary>Count of player missions missing migration-6 filter columns (ship == -1). Mirrors Go CountPendingFilterCols.</summary>
    public async Task<int?> CountPendingFilterColsAsync(string eid) {
        var rows = await PlayerMetaRowsAsync(eid);
        return rows.Count(r => r.Ship == -1);
    }

    /// <summary>Fast path: display rows from stored filter columns only (ship != -1), ordered by start_timestamp.</summary>
    public async Task<IReadOnlyList<IMissionRow>?> GetPlayerMissionMetaAsync(string eid) {
        var rows = await PlayerMetaRowsAsync(eid);
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
            // Bounded-concurrency batches (order preserved); decode is CPU-bound protobuf-net work.
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

    // Bounded LRU of decoded single-mission responses, keyed (playerId, missionId). Only the hot
    // repeat path (overlay + prev/next) uses it; callers treat the response as read-only.
    private const int DecodeCacheCap = 256;
    private readonly Lock _decodeGate = new();
    private readonly LinkedList<(string Key, CompleteMissionResponse Value)> _decodeLru = new();
    private readonly Dictionary<string, LinkedListNode<(string Key, CompleteMissionResponse Value)>> _decodeIndex = new(StringComparer.Ordinal);

    private static string DecodeKey(string playerId, string missionId) => playerId + " " + missionId;

    private CompleteMissionResponse? DecodeCacheGet(string key) {
        lock (_decodeGate) {
            if (!_decodeIndex.TryGetValue(key, out var node)) {
                return null;
            }
            _decodeLru.Remove(node);
            _decodeLru.AddFirst(node);
            return node.Value.Value;
        }
    }

    private void DecodeCachePut(string key, CompleteMissionResponse value) {
        lock (_decodeGate) {
            if (_decodeIndex.TryGetValue(key, out var existing)) {
                _decodeLru.Remove(existing);
                _decodeIndex.Remove(key);
            }
            var node = _decodeLru.AddFirst((key, value));
            _decodeIndex[key] = node;
            while (_decodeIndex.Count > DecodeCacheCap) {
                var last = _decodeLru.Last!;
                _decodeLru.RemoveLast();
                _decodeIndex.Remove(last.Value.Key);
            }
        }
    }

    private void DecodeCacheEvict(string key) {
        lock (_decodeGate) {
            if (_decodeIndex.TryGetValue(key, out var node)) {
                _decodeLru.Remove(node);
                _decodeIndex.Remove(key);
            }
        }
    }

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

                if (_packer.TryComputeMissionFilterCols(row.StartTimestamp, decoded, out var cols)) {
                    await _db.PutAsync(IndexedDbStores.Mission, WithCols(row, cols)).ConfigureAwait(false);
                    DecodeCacheEvict(DecodeKey(eid, row.MissionId));
                }
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

    private readonly HashSet<string> _dropsBackfilling = [];

    // Port of the Go startup goroutine db.BackfillArtifactDrops, but per-account and
    // triggered on load instead of at process startup: populates artifact_drops for
    // missions stored before that table existed, or fetched by an older app version
    // that skipped the write. A mission with zero real drops gets a drop_index=-1
    // sentinel row so it is never re-decoded on a later call (GetAllPlayerDropsAsync
    // filters DropIndex &lt; 0 back out).
    public void QueueArtifactDropsBackfill(string playerId) {
        if (_dropsBackfilling.Add(playerId))
            _ = BackfillArtifactDropsAsync(playerId);
    }

    private async Task BackfillArtifactDropsAsync(string playerId) {
        try {
            var missionIds = await GetCompleteMissionIdsAsync(playerId).ConfigureAwait(false);
            if (missionIds is null)
                return;
            var stored = await GetStoredPlayerDropsAsync(playerId).ConfigureAwait(false);
            if (stored is null)
                return;
            var haveRows = stored.Select(d => d.MissionId).ToHashSet();

            foreach (var missionId in missionIds) {
                if (haveRows.Contains(missionId))
                    continue;

                CompleteMissionResponse decoded;
                try { decoded = await GetCompleteMissionAsync(playerId, missionId).ConfigureAwait(false) ?? throw new InvalidOperationException(); } catch { continue; }

                var drops = ArtifactDrops.Build(decoded);
                if (drops.Count == 0) {
                    await _db.PutAsync(IndexedDbStores.ArtifactDrops, new ArtifactDropRow {
                        MissionId = missionId,
                        PlayerId = playerId,
                        DropIndex = -1,
                    }).ConfigureAwait(false);
                    continue;
                }

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
                await _db.PutManyAsync(IndexedDbStores.ArtifactDrops, rows).ConfigureAwait(false);
            }
        } finally {
            _dropsBackfilling.Remove(playerId);
        }
    }

    /// <summary>
    /// First-contact backup, raw payload gzipped (Go db.InsertBackup). A positive
    /// <paramref name="minimumGap"/> skips the write when the existing backup is newer
    /// than the gap (the 12h dedup); the <c>backup</c> store keys on <c>player_id</c>, one row per player.
    /// </summary>
    public async Task InsertBackupAsync(string playerId, double timestamp, byte[] rawPayload, TimeSpan minimumGap) {
        if (minimumGap > TimeSpan.Zero) {
            var existing = await _db.GetAsync<BackupRow>(IndexedDbStores.Backup, playerId);
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
        await _db.PutAsync(IndexedDbStores.Backup, row);
    }

    /// <summary>
    /// Inserts one fetched mission plus its artifact_drops rows. <paramref name="rawPayload"/>
    /// is the raw AuthenticatedMessage wire, gzipped into <c>complete_payload</c> exactly as
    /// <see cref="GetCompleteMissionAsync"/> reads it back.
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
        await _db.PutAsync(IndexedDbStores.Mission, row);
        DecodeCacheEvict(DecodeKey(playerId, missionId));

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
            await _db.PutManyAsync(IndexedDbStores.ArtifactDrops, rows);
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
        var rows = await _db.GetAllByIndexAsync<MissionRow>(IndexedDbStores.Mission, IndexedDbStores.PlayerIdIndex, playerId);
        return [.. rows];
    }

    // Payload-free metadata rows; the projected read does not pull complete_payload.
    private async Task<List<MissionMetaRow>> PlayerMetaRowsAsync(string playerId) {
        var rows = await _db.GetAllByIndexProjectedAsync<MissionMetaRow>(IndexedDbStores.Mission, IndexedDbStores.PlayerIdIndex, playerId);
        return [.. rows];
    }

    public async Task<IReadOnlyList<StoredDrop>?> GetStoredPlayerDropsAsync(string playerId) {
        try {
            var rows = await _db.GetAllByIndexAsync<ArtifactDropRow>(IndexedDbStores.ArtifactDrops, IndexedDbStores.PlayerIdIndex, playerId);
            return [.. rows.Select(r => new StoredDrop(r.MissionId, r.ArtifactId, r.Level, r.Rarity, r.DropIndex))];
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

    private static MissionMeta ToMeta(MissionMetaRow row) => new() {
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
