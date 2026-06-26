using System.Text.Json;
using System.Text.Json.Serialization;

namespace EggLedger.Domain.Eiafx;

/// <summary>
/// Summary of an artifact family for the UI. Port of Go eiafx.FamilyMeta.
/// </summary>
public sealed record FamilyMeta(string Id, string Name);

/// <summary>
/// Crafting weights and artifact families from the embedded eiafx-data-min.json. Port of Go
/// eiafx/data.go, embedded-only (the Go cache/download path is a host concern, omitted here).
/// </summary>
public static class EiafxData {
    private const string ResourceName = "EggLedger.Domain.Resources.eiafx-data-min.json";

    private static readonly Lazy<ParsedData> _data = new(LoadEmbedded);

    /// <summary>
    /// Maps (afxId, afxLevel) to the T1-equivalent base item weight (base items have weight 1.0).
    /// Mirrors Go eiafx.CraftingWeights; the cycle sentinel value 0.0 is preserved exactly.
    /// </summary>
    public static IReadOnlyDictionary<(int AfxId, int AfxLevel), double> CraftingWeights => _data.Value.CraftingWeights;

    /// <summary>
    /// Maps family id to the afx_id integers in that family (from child_afx_ids).
    /// Mirrors Go eiafx.FamilyAFXIds.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<int>> FamilyAfxIds => _data.Value.FamilyAfxIds;

    /// <summary>Ordered list of families for the UI dropdown. Mirrors Go eiafx.Families.</summary>
    public static IReadOnlyList<FamilyMeta> Families => _data.Value.Families;

    /// <summary>
    /// Crafting weight for a tier, falling back to 1.0 when the key is absent or the cycle-sentinel
    /// (0). Port of Go reports.craftingWeight.
    /// </summary>
    public static double CraftingWeightOrOne(long afxId, long afxLevel) {
        if (CraftingWeights.TryGetValue(((int)afxId, (int)afxLevel), out var w) && w != 0d) {
            return w;
        }
        return 1d;
    }

    private sealed record ParsedData(
        IReadOnlyDictionary<(int, int), double> CraftingWeights,
        IReadOnlyDictionary<string, IReadOnlyList<int>> FamilyAfxIds,
        IReadOnlyList<FamilyMeta> Families);

    private static ParsedData LoadEmbedded() {
        var asm = typeof(EiafxData).Assembly;
        using var stream = asm.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"embedded eiafx data resource not found: {ResourceName}");
        var cfg = JsonSerializer.Deserialize(stream, EiafxDataJsonContext.Default.ArtifactDataConfig)
            ?? throw new InvalidOperationException("eiafx data json deserialized to null");
        return Parse(cfg);
    }

    // Port of Go parseDataConfig.
    private static ParsedData Parse(ArtifactDataConfig cfg) {
        var families = cfg.Families ?? [];

        // Flat map from (afxId, afxLevel) -> tier for weight traversal.
        var tierMap = new Dictionary<(int, int), TierData>();
        foreach (var fam in families) {
            foreach (var t in fam.Tiers ?? []) {
                tierMap[(t.AfxId, t.AfxLevel)] = t;
            }
        }

        // Memoized recursive weight computation.
        var memo = new Dictionary<(int, int), double>();
        double ComputeWeight(int afxId, int afxLevel) {
            var key = (afxId, afxLevel);
            if (memo.TryGetValue(key, out var cached)) {
                return cached;
            }
            if (!tierMap.TryGetValue(key, out var t) || t.Recipe == null || t.Recipe.Count == 0) {
                memo[key] = 1.0;
                return 1.0;
            }
            // cycle sentinel: prevents infinite recursion if data has a cycle
            memo[key] = 0;
            var w = 0.0;
            foreach (var ing in t.Recipe) {
                w += ing.Count * ComputeWeight(ing.AfxId, ing.AfxLevel);
            }
            memo[key] = w;
            return w;
        }

        foreach (var key in tierMap.Keys) {
            ComputeWeight(key.Item1, key.Item2);
        }

        var fids = new Dictionary<string, IReadOnlyList<int>>(families.Count);
        foreach (var f in families) {
            fids[f.Id] = f.ChildAfxIds ?? [];
        }

        var fams = new List<FamilyMeta>(families.Count);
        foreach (var f in families) {
            fams.Add(new FamilyMeta(f.Id, f.Name));
        }

        return new ParsedData(memo, fids, fams);
    }

    internal sealed class ArtifactDataConfig {
        [JsonPropertyName("families")]
        public List<FamilyData>? Families { get; set; }
    }

    internal sealed class FamilyData {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("child_afx_ids")]
        public List<int>? ChildAfxIds { get; set; }
        [JsonPropertyName("tiers")]
        public List<TierData>? Tiers { get; set; }
    }

    internal sealed class TierData {
        [JsonPropertyName("afx_id")]
        public int AfxId { get; set; }
        [JsonPropertyName("afx_level")]
        public int AfxLevel { get; set; }
        [JsonPropertyName("recipe")]
        public List<IngredientData>? Recipe { get; set; }
    }

    internal sealed class IngredientData {
        [JsonPropertyName("afx_id")]
        public int AfxId { get; set; }
        [JsonPropertyName("afx_level")]
        public int AfxLevel { get; set; }
        [JsonPropertyName("count")]
        public int Count { get; set; }
    }
}

[JsonSerializable(typeof(EiafxData.ArtifactDataConfig))]
internal sealed partial class EiafxDataJsonContext : JsonSerializerContext;
