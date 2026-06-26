using System.IO.Compression;
using EggLedger.Domain.Api;
using EggLedger.Web.Data;
using Ei;
using ProtoBuf;

namespace EggLedger.Web.Tests.Data;

public sealed class IndexedDbMissionStoreTests {
    private static (IndexedDbMissionStore Store, FakeIndexedDb Db) Make() {
        var db = new FakeIndexedDb();
        var decoder = new LocalApiPayloadDecoder(new ApiClient());
        return (new IndexedDbMissionStore(db, decoder), db);
    }

    private static MissionRow MissionMeta(string playerId, string missionId, double start, int ship = 0) => new() {
        PlayerId = playerId,
        MissionId = missionId,
        StartTimestamp = start,
        Ship = ship,
        ReturnTimestamp = start + 100,
    };

    /// <summary>
    /// Builds a stored complete_payload (gzipped AuthenticatedMessage wrapping a
    /// serialized CompleteMissionResponse), mirroring the on-disk format the Go fetch pipeline writes.
    /// </summary>
    private static byte[] PackPayload(CompleteMissionResponse resp) {
        using var inner = new MemoryStream();
        Serializer.Serialize(inner, resp);
        var auth = new AuthenticatedMessage { Message = inner.ToArray(), Compressed = false };

        using var authBytes = new MemoryStream();
        Serializer.Serialize(authBytes, auth);

        using var gzipped = new MemoryStream();
        using (var gz = new GZipStream(gzipped, CompressionMode.Compress, leaveOpen: true)) {
            var raw = authBytes.ToArray();
            gz.Write(raw, 0, raw.Length);
        }
        return gzipped.ToArray();
    }

    [Fact]
    public async Task GetCompleteMissionIdsAsync_ReturnsOnlyPlayerRows_OrderedByStart() {
        var (store, db) = Make();
        db.Seed("mission", MissionMeta("EI1", "m2", start: 200));
        db.Seed("mission", MissionMeta("EI1", "m1", start: 100));
        db.Seed("mission", MissionMeta("EI2", "other", start: 50));

        var ids = await store.GetCompleteMissionIdsAsync("EI1");

        Assert.Equal(new[] { "m1", "m2" }, ids);
    }

    [Fact]
    public async Task GetCompleteMissionIdsAsync_EmptyWhenNoRows() {
        var (store, _) = Make();
        var ids = await store.GetCompleteMissionIdsAsync("EI1");
        Assert.Empty(ids!);
    }

    [Fact]
    public async Task GetPlayerMissionStatsAsync_CountsAndMaxReturn() {
        var (store, db) = Make();
        db.Seed("mission", MissionMeta("EI1", "m1", start: 100));
        db.Seed("mission", MissionMeta("EI1", "m2", start: 500));
        db.Seed("mission", MissionMeta("EI2", "x", start: 999));

        var stats = await store.GetPlayerMissionStatsAsync("EI1");

        Assert.NotNull(stats);
        Assert.Equal(2, stats!.Value.Count);
        Assert.Equal(600, stats.Value.MaxReturnTimestamp);
    }

    [Fact]
    public async Task GetPlayerMissionStatsAsync_ZeroWhenNoRows() {
        var (store, _) = Make();
        var stats = await store.GetPlayerMissionStatsAsync("EI1");
        Assert.NotNull(stats);
        Assert.Equal(0, stats!.Value.Count);
        Assert.Equal(0, stats.Value.MaxReturnTimestamp);
    }

    [Fact]
    public async Task CountPendingFilterColsAsync_CountsShipMinusOne() {
        var (store, db) = Make();
        db.Seed("mission", MissionMeta("EI1", "done", start: 1, ship: 0));
        db.Seed("mission", MissionMeta("EI1", "pending1", start: 2, ship: -1));
        db.Seed("mission", MissionMeta("EI1", "pending2", start: 3, ship: -1));

        Assert.Equal(2, await store.CountPendingFilterColsAsync("EI1"));
    }

    [Fact]
    public async Task GetCompleteMissionAsync_DecodesGzippedAuthenticatedPayload() {
        var (store, db) = Make();
        var resp = new CompleteMissionResponse {
            Success = true,
            Info = new MissionInfo { Identifier = "m1", Ship = MissionInfo.Spaceship.Henerprise },
        };
        resp.Artifacts.Add(new CompleteMissionResponse.SecureArtifactSpec {
            Spec = new ArtifactSpec { name = ArtifactSpec.Name.TachyonDeflector },
        });

        db.Seed("mission", new MissionRow {
            PlayerId = "EI1",
            MissionId = "m1",
            StartTimestamp = 12345,
            Ship = 0,
            CompletePayload = PackPayload(resp),
        });

        var got = await store.GetCompleteMissionAsync("EI1", "m1");

        Assert.NotNull(got);
        Assert.True(got!.Success);
        Assert.Equal("m1", got.Info!.Identifier);
        Assert.Equal(MissionInfo.Spaceship.Henerprise, got.Info.Ship);
        Assert.Single(got.Artifacts);
        Assert.Equal(ArtifactSpec.Name.TachyonDeflector, got.Artifacts[0].Spec!.name);
        // StartTimeDerived is restored from the row, not the payload.
        Assert.Equal(12345, got.Info.StartTimeDerived);
    }

    [Fact]
    public async Task GetCompleteMissionAsync_NullOnCacheMiss() {
        var (store, db) = Make();
        db.Seed("mission", MissionMeta("EI1", "m1", start: 1));
        Assert.Null(await store.GetCompleteMissionAsync("EI1", "nope"));
    }

    [Fact]
    public async Task StreamPlayerCompleteMissionsAsync_VisitsDecodedMissionsInOrder() {
        var (store, db) = Make();
        db.Seed("mission", PayloadRow("EI1", "m2", start: 200, identifier: "m2"));
        db.Seed("mission", PayloadRow("EI1", "m1", start: 100, identifier: "m1"));

        var seen = new List<string>();
        bool ok = await store.StreamPlayerCompleteMissionsAsync("EI1", cm => seen.Add(cm.Info!.Identifier));

        Assert.True(ok);
        Assert.Equal(new[] { "m1", "m2" }, seen);
    }

    private static MissionRow PayloadRow(string playerId, string missionId, double start, string identifier) {
        var resp = new CompleteMissionResponse {
            Success = true,
            Info = new MissionInfo { Identifier = identifier },
        };
        return new MissionRow {
            PlayerId = playerId,
            MissionId = missionId,
            StartTimestamp = start,
            Ship = 0,
            CompletePayload = PackPayload(resp),
        };
    }
}
