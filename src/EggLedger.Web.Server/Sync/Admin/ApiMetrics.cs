using System.Collections.Concurrent;

namespace EggLedger.Web.Server.Sync.Admin;

// In-process API-rate metrics: a 60-slot ring of per-minute request buckets (last hour)
// plus a lifetime per-path tally. Singleton, thread-safe via Interlocked; resets on restart.
public sealed class ApiMetrics(TimeProvider time) {
    public const int Minutes = 60;

    private sealed class Bucket {
        public long Epoch;
        public int Total;
    }

    private readonly Bucket[] _ring = [.. Enumerable.Range(0, Minutes).Select(_ => new Bucket())];
    private readonly ConcurrentDictionary<string, long> _byPath = new(StringComparer.Ordinal);

    private long NowMinute() => time.GetUtcNow().ToUnixTimeSeconds() / 60;

    public void Record(string path) {
        var minute = NowMinute();
        var b = _ring[(int)(minute % Minutes)];
        if (Interlocked.Read(ref b.Epoch) != minute) {
            lock (b) {
                if (b.Epoch != minute) { b.Epoch = minute; b.Total = 0; }
            }
        }
        Interlocked.Increment(ref b.Total);
        _byPath.AddOrUpdate(Normalize(path), 1, (_, v) => v + 1);
    }

    // Collapse the id-bearing blob path so the tally does not explode per blob name.
    private static string Normalize(string path) =>
        path.StartsWith("/api/v1/blobs/", StringComparison.Ordinal) ? "/api/v1/blobs/{name}" : path;

    public IReadOnlyList<MinutePoint> SnapshotMinutes() {
        var now = NowMinute();
        var pts = new List<MinutePoint>(Minutes);
        for (var i = Minutes - 1; i >= 0; i--) {
            var minute = now - i;
            var b = _ring[(int)(minute % Minutes)];
            var total = Volatile.Read(ref b.Epoch) == minute ? b.Total : 0;
            pts.Add(new MinutePoint(minute * 60, total));
        }
        return pts;
    }

    public IReadOnlyList<PathCount> SnapshotPaths() =>
        [.. _byPath.Select(kv => new PathCount(kv.Key, kv.Value)).OrderByDescending(p => p.Count)];

    public sealed record MinutePoint(long MinuteEpochSeconds, int Total);
    public sealed record PathCount(string Path, long Count);
}
