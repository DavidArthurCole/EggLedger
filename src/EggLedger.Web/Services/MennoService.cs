using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ei;
using EggLedger.Domain.Eiafx;
using EggLedger.Domain.Reports;
using EggLedger.Domain.Util;

namespace EggLedger.Web.Services;

/// <summary>
/// Decode helpers for the Menno community drop-rate payload. Separated from the
/// HTTP fetch so the typed decode and its loud-failure validation can be unit
/// tested without a network stub.
/// </summary>
public static class MennoDecode
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false,
        // A renamed top-level field is harmless (it just does not bind), but an
        // unexpected enum-as-string or type mismatch must surface, not be coerced.
        NumberHandling = JsonNumberHandling.Strict,
    };

    /// <summary>
    /// Decodes the raw Menno JSON array into typed records and validates that the
    /// required nested objects bound. Throws <see cref="MennoSchemaException"/>
    /// when the payload is empty, is not a JSON array, or an item is missing a
    /// required nested field. This is the loud-failure guarantee: a schema drift
    /// produces an exception rather than silently zeroed data.
    /// </summary>
    public static List<ConfigurationItem> Decode(ReadOnlySpan<byte> json)
    {
        List<ConfigurationItem>? items;
        try
        {
            items = JsonSerializer.Deserialize<List<ConfigurationItem>>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new MennoSchemaException("Menno data response was not valid JSON or did not match the expected shape", ex);
        }

        if (items is null || items.Count == 0)
        {
            throw new MennoSchemaException("Menno data response was empty or schema has changed");
        }

        for (int i = 0; i < items.Count; i++)
        {
            Validate(items[i], i);
        }
        return items;
    }

    /// <summary>
    /// Verifies the required nested objects of a single item bound. STJ leaves a
    /// missing or renamed nested object null; the comparison math dereferences
    /// these, so a null here is exactly the silent-drop the Go path suffered.
    /// </summary>
    private static void Validate(ConfigurationItem item, int index)
    {
        var sc = item.ShipConfiguration
            ?? throw Missing(index, "shipConfiguration");
        if (sc.ShipType is null) throw Missing(index, "shipConfiguration.shipType");
        if (sc.ShipDurationType is null) throw Missing(index, "shipConfiguration.shipDurationType");
        if (sc.TargetArtifact is null) throw Missing(index, "shipConfiguration.targetArtifact");

        var ac = item.ArtifactConfiguration
            ?? throw Missing(index, "artifactConfiguration");
        if (ac.ArtifactType is null) throw Missing(index, "artifactConfiguration.artifactType");
        if (ac.ArtifactRarity is null) throw Missing(index, "artifactConfiguration.artifactRarity");
    }

    private static MennoSchemaException Missing(int index, string field) =>
        new($"Menno item {index} is missing required field '{field}'; schema may have changed");
}

/// <summary>
/// Raised when the Menno payload cannot be decoded into the typed model, or an
/// item is missing a required field. Surfacing this is the point of the typed
/// port: the Go loose decode hid these failures.
/// </summary>
public sealed class MennoSchemaException : Exception
{
    public MennoSchemaException(string message) : base(message) { }
    public MennoSchemaException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Community drop-rate stats client. C# port of the Go menno package
/// (mennodata.go fetch + mennoreports.go ExecuteComparison), scoped to the
/// browser.
///
/// <para>Fetches the gzipped community aggregate from the Azure blob, decodes it
/// into the strongly-typed <see cref="ConfigurationItem"/> model (failing loudly
/// on schema drift), and caches it in memory. The Go desktop app cached the
/// decoded JSON to internal/menno-data.json; the browser has no writable disk, so
/// the cache is in-memory only and lives for the lifetime of the scoped service.</para>
///
/// <para>Scope: read-only consumption (download + decode + ExecuteComparison).
/// The community-contribution upload flow is a separate feature and is not
/// ported here.</para>
/// </summary>
public sealed class MennoService
{
    /// <summary>
    /// Azure blob endpoint for the gzipped community aggregate. Mirrors the Go
    /// constant in mennodata.go.
    /// </summary>
    public const string DataUrl =
        "https://eggincdatacollectionsa.blob.core.windows.net/mission-data/all-data.json.gz";

    private readonly HttpClient _http;
    private List<ConfigurationItem>? _cache;
    private Task<IReadOnlyList<ConfigurationItem>>? _inFlight;

    public MennoService(HttpClient http) => _http = http;

    /// <summary>True once a successful download has populated the in-memory cache.</summary>
    public bool HasData => _cache is { Count: > 0 };

    /// <summary>
    /// Ensures the in-memory cache is populated, downloading once. Concurrent
    /// callers (several Menno-enabled report cards rendering at once) share a single
    /// in-flight download rather than each hitting the network. Returns immediately
    /// when the cache is already populated. A failed download is not cached, so a
    /// later call retries.
    /// </summary>
    public Task<IReadOnlyList<ConfigurationItem>> EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (_cache is { Count: > 0 } cached)
        {
            return Task.FromResult<IReadOnlyList<ConfigurationItem>>(cached);
        }
        _inFlight ??= LoadOnceAsync(cancellationToken);
        return _inFlight;
    }

    private async Task<IReadOnlyList<ConfigurationItem>> LoadOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _inFlight = null;
        }
    }

    /// <summary>
    /// Downloads the gzipped Menno aggregate, decompresses it, typed-decodes it
    /// (loud on schema drift), and stores it in the in-memory cache. Returns the
    /// decoded items. Throws <see cref="MennoSchemaException"/> on a bad payload;
    /// network and decompression errors propagate as their native exceptions.
    /// </summary>
    public async Task<IReadOnlyList<ConfigurationItem>> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await using var compressed = await _http.GetStreamAsync(DataUrl, cancellationToken).ConfigureAwait(false);
        await using var gz = new GZipStream(compressed, CompressionMode.Decompress);
        using var ms = new MemoryStream();
        await gz.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);

        var items = MennoDecode.Decode(ms.GetBuffer().AsSpan(0, (int)ms.Length));
        _cache = items;
        return items;
    }

    /// <summary>The cached items, or null if no successful refresh has run.</summary>
    public IReadOnlyList<ConfigurationItem>? CachedItems => _cache;

    /// <summary>
    /// Runs a Menno community-data comparison for the given report definition over
    /// the supplied community items, returning a <see cref="ReportResult"/> matrix
    /// matching the user's report dimensions. C# port of Go
    /// menno.ExecuteComparison. Returns null when the report is ineligible (Menno
    /// disabled, non-artifacts subject, non-comparable group-bys, empty inputs).
    ///
    /// <para>rawRowLabels / rawColLabels are the pre-FormatLabel integer strings
    /// the report executor emits, used to map display cells back to Menno record
    /// ids without string matching.</para>
    /// </summary>
    public static ReportResult? ExecuteComparison(
        ReportDefinition def,
        IReadOnlyList<ConfigurationItem> items,
        IReadOnlyList<string> rawRowLabels,
        IReadOnlyList<string> rawColLabels)
    {
        if (!def.MennoEnabled)
        {
            return null;
        }
        if (def.Subject != "artifacts")
        {
            return null;
        }
        if (!Report.MennoComparableGroupBy(def.GroupBy) || !Report.MennoComparableGroupBy(def.SecondaryGroupBy))
        {
            return null;
        }
        if (items.Count == 0 || rawRowLabels.Count == 0 || rawColLabels.Count == 0)
        {
            return null;
        }

        var rowMatcher = MatcherFor(def.GroupBy);
        var colMatcher = MatcherFor(def.SecondaryGroupBy);
        if (rowMatcher is null || colMatcher is null)
        {
            return null;
        }

        var familySet = FamilyAfxIdSet(def.FamilyWeight);

        int nR = rawRowLabels.Count;
        int nC = rawColLabels.Count;

        string pctMode = def.NormalizeBy;
        bool isPct = pctMode is "row_pct" or "col_pct" or "global_pct";

        var familyDrops = new double[nR * nC];
        var estimatedMissions = new double[nR * nC];

        foreach (var item in items)
        {
            for (int r = 0; r < nR; r++)
            {
                if (!rowMatcher(item, rawRowLabels[r]))
                {
                    continue;
                }
                for (int c = 0; c < nC; c++)
                {
                    if (!colMatcher(item, rawColLabels[c]))
                    {
                        continue;
                    }
                    int idx = (r * nC) + c;
                    if (!isPct)
                    {
                        double itemCap = LevelAdjustedCapacityFor(
                            item.ShipConfiguration!.ShipType!.Id,
                            item.ShipConfiguration.ShipDurationType!.Id,
                            item.ShipConfiguration.Level);
                        estimatedMissions[idx] += item.TotalDrops / itemCap;
                    }
                    int artifactTypeId = item.ArtifactConfiguration!.ArtifactType!.Id;
                    if (familySet is null || familySet.Contains(artifactTypeId))
                    {
                        double w = 1.0;
                        if (def.FamilyWeight != "")
                        {
                            double cw = EiafxData.CraftingWeights.TryGetValue(
                                (artifactTypeId, item.ArtifactConfiguration.ArtifactLevel), out var found)
                                ? found
                                : 0;
                            if (cw > 0)
                            {
                                w = cw;
                            }
                        }
                        familyDrops[idx] += item.TotalDrops * w;
                    }
                }
            }
        }

        string shipAxis = "";
        string durAxis = "";
        if (def.GroupBy == "ship_type")
        {
            shipAxis = "row";
        }
        else if (def.SecondaryGroupBy == "ship_type")
        {
            shipAxis = "col";
        }
        if (def.GroupBy == "duration_type")
        {
            durAxis = "row";
        }
        else if (def.SecondaryGroupBy == "duration_type")
        {
            durAxis = "col";
        }

        var matrixValues = new double[nR * nC];

        bool canComputeAirtime = shipAxis != "" && durAxis != "" && !isPct;
        double[]? airtimeMatrixValues = canComputeAirtime ? new double[nR * nC] : null;

        for (int r = 0; r < nR; r++)
        {
            for (int c = 0; c < nC; c++)
            {
                int idx = (r * nC) + c;
                double fd = familyDrops[idx];

                if (isPct)
                {
                    matrixValues[idx] = fd;
                    continue;
                }

                double estMissions = estimatedMissions[idx];
                if (estMissions <= 0 || double.IsNaN(estMissions) || double.IsInfinity(estMissions))
                {
                    matrixValues[idx] = 0;
                    continue;
                }

                matrixValues[idx] = fd / estMissions;

                if (canComputeAirtime)
                {
                    int shipId = 0;
                    int durId = 0;
                    if (shipAxis == "row")
                    {
                        shipId = ParseIntOrZero(rawRowLabels[r]);
                    }
                    else if (shipAxis == "col")
                    {
                        shipId = ParseIntOrZero(rawColLabels[c]);
                    }
                    if (durAxis == "row")
                    {
                        durId = ParseIntOrZero(rawRowLabels[r]);
                    }
                    else if (durAxis == "col")
                    {
                        durId = ParseIntOrZero(rawColLabels[c]);
                    }
                    double nominalSecs = DurationSecondsFor(shipId, durId);
                    if (nominalSecs > 0)
                    {
                        airtimeMatrixValues![idx] = fd / estMissions / (nominalSecs / 3600.0);
                    }
                }
            }
        }

        if (isPct)
        {
            Matrix.Apply2DPctNormalization(matrixValues, nR, nC, pctMode);
        }

        var rowLabels = new List<string>(nR);
        var colLabels = new List<string>(nC);
        for (int i = 0; i < nR; i++)
        {
            rowLabels.Add(Labels.FormatLabel(def.GroupBy, rawRowLabels[i]));
        }
        for (int i = 0; i < nC; i++)
        {
            colLabels.Add(Labels.FormatLabel(def.SecondaryGroupBy, rawColLabels[i]));
        }

        return new ReportResult
        {
            RowLabels = rowLabels,
            ColLabels = colLabels,
            MatrixValues = matrixValues.ToList(),
            Is2D = true,
            IsFloat = true,
            Weight = def.Weight,
            RawRowLabels = rawRowLabels.ToList(),
            RawColLabels = rawColLabels.ToList(),
            AirtimeMatrixValues = airtimeMatrixValues?.ToList(),
        };
    }

    private delegate bool MennoMatcher(ConfigurationItem item, string rawVal);

    /// <summary>Builds a cell-matcher for a group-by dimension. Port of Go mennoMatcherFor.</summary>
    private static MennoMatcher? MatcherFor(string groupBy) => groupBy switch
    {
        "ship_type" => (item, raw) => TryInt(raw, out var v) && item.ShipConfiguration!.ShipType!.Id == v,
        "duration_type" => (item, raw) => TryInt(raw, out var v) && item.ShipConfiguration!.ShipDurationType!.Id == v,
        "level" => (item, raw) => TryInt(raw, out var v) && item.ShipConfiguration!.Level == v,
        "mission_target" => (item, raw) => TryInt(raw, out var v) && item.ShipConfiguration!.TargetArtifact!.Id == v,
        "artifact_name" => (item, raw) => TryInt(raw, out var v) && item.ArtifactConfiguration!.ArtifactType!.Id == v,
        "rarity" => (item, raw) => TryInt(raw, out var v) && item.ArtifactConfiguration!.ArtifactRarity!.Id == v,
        "tier" => (item, raw) => TryInt(raw, out var v) && item.ArtifactConfiguration!.ArtifactLevel == v,
        _ => null,
    };

    /// <summary>Set of artifact type ids in a family, or null (match all) when empty. Port of Go familyAFXIDSet.</summary>
    private static HashSet<int>? FamilyAfxIdSet(string familyWeight)
    {
        if (familyWeight == "")
        {
            return null;
        }
        if (!EiafxData.FamilyAfxIds.TryGetValue(familyWeight, out var ids))
        {
            return null;
        }
        return new HashSet<int>(ids);
    }

    /// <summary>
    /// Level-adjusted nominal capacity for a (ship, duration, level). Port of Go
    /// mennoLevelAdjustedCapacityFor: base + levelCapacityBump * level, with a 4.0
    /// fallback when the lookup misses or the config is unavailable.
    /// </summary>
    private static double LevelAdjustedCapacityFor(int shipId, int durationId, int level)
    {
        var ship = (MissionInfo.Spaceship)shipId;
        var dur = (MissionInfo.DurationType)durationId;
        foreach (var mp in EiafxConfig.Config.mission_parameters)
        {
            if (mp.Ship != ship)
            {
                continue;
            }
            foreach (var d in mp.Durations)
            {
                if (d.DurationType == dur)
                {
                    double cap = d.Capacity + (d.LevelCapacityBump * (double)level);
                    if (cap > 0)
                    {
                        return cap;
                    }
                }
            }
        }
        return 4;
    }

    /// <summary>Nominal flight seconds for a (ship, duration), or 0 if absent. Port of Go mennoDurationSecondsFor.</summary>
    private static double DurationSecondsFor(int shipId, int durationId)
    {
        var ship = (MissionInfo.Spaceship)shipId;
        var dur = (MissionInfo.DurationType)durationId;
        foreach (var mp in EiafxConfig.Config.mission_parameters)
        {
            if (mp.Ship != ship)
            {
                continue;
            }
            foreach (var d in mp.Durations)
            {
                if (d.DurationType == dur)
                {
                    return d.Seconds;
                }
            }
        }
        return 0;
    }

    private static bool TryInt(string raw, out int value) =>
        int.TryParse(raw, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out value);

    private static int ParseIntOrZero(string raw) => TryInt(raw, out var v) ? v : 0;
}
