using EggLedger.Domain.MissionQuery;

namespace EggLedger.Web.Missions;

public sealed class LifetimeData {
    public int MissionCount { get; set; }
    public List<DropLike> Artifacts { get; set; } = [];
    public List<DropLike> Stones { get; set; } = [];
    public List<DropLike> StoneFragments { get; set; } = [];
    public List<DropLike> Ingredients { get; set; } = [];
}

/// <summary>Folds every mission's drops into four spec-type buckets, combining identical drops. CAUTION: SpecType must be exactly "Artifact"/"Stone"/"StoneFragment"/"Ingredient" (verbatim from MissionQueryHandlers.ShapeDrop) or drops are silently filtered out.</summary>
public static class LifetimeAggregator {
    /// <summary>Merge key is id_level_rarity per bucket; first occurrence is the representative, later identical drops bump Count. MissionCount is the dictionary entry count, so pass only the missions you intend to include.</summary>
    public static LifetimeData Aggregate(IReadOnlyDictionary<string, List<MissionDrop>> dropsByMission) {
        var artifacts = new Bucket();
        var stones = new Bucket();
        var stoneFragments = new Bucket();
        var ingredients = new Bucket();

        foreach (var kvp in dropsByMission) {
            foreach (var drop in kvp.Value) {
                var bucket = drop.SpecType switch {
                    "Artifact" => artifacts,
                    "Stone" => stones,
                    "StoneFragment" => stoneFragments,
                    "Ingredient" => ingredients,
                    _ => null,
                };
                bucket?.Merge(drop);
            }
        }

        return new LifetimeData {
            MissionCount = dropsByMission.Count,
            Artifacts = artifacts.Items,
            Stones = stones.Items,
            StoneFragments = stoneFragments.Items,
            Ingredients = ingredients.Items,
        };
    }

    // Port of MissionOverlay.vue mergeDropArrays: merge by id_level_rarity, summing counts; first occurrence is the representative (full copy).
    public static List<DropLike> MergeDropArrays(IEnumerable<IReadOnlyList<DropLike>> arrays) {
        var map = new Dictionary<string, DropLike>();
        var order = new List<string>();
        foreach (var arr in arrays) {
            foreach (var item in arr) {
                string key = item.Id + "_" + item.Level + "_" + item.Rarity;
                if (map.TryGetValue(key, out var existing)) {
                    existing.Count += item.Count;
                } else {
                    map[key] = new DropLike {
                        Id = item.Id,
                        Name = item.Name,
                        GameName = item.GameName,
                        EffectString = item.EffectString,
                        Level = item.Level,
                        Rarity = item.Rarity,
                        Quality = item.Quality,
                        IvOrder = item.IvOrder,
                        SpecType = item.SpecType,
                        Count = item.Count,
                    };
                    order.Add(key);
                }
            }
        }
        var result = new List<DropLike>(order.Count);
        foreach (var key in order) {
            result.Add(map[key]);
        }
        return result;
    }

    // Dedupe-on-insert bucket: ordered list plus a key index (mirrors Vue LifetimeBucket: arr + index Map).
    private sealed class Bucket {
        public List<DropLike> Items { get; } = [];
        private readonly Dictionary<string, DropLike> _index = [];

        public void Merge(MissionDrop drop) {
            string key = drop.Id + "_" + drop.Level + "_" + drop.Rarity;
            if (_index.TryGetValue(key, out var existing)) {
                existing.Count += 1;
                return;
            }
            var rep = new DropLike {
                Id = drop.Id,
                Name = drop.Name,
                GameName = drop.GameName,
                EffectString = drop.EffectString,
                Level = drop.Level,
                Rarity = drop.Rarity,
                Quality = drop.Quality,
                IvOrder = drop.IVOrder,
                SpecType = drop.SpecType,
                Count = 1,
            };
            _index[key] = rep;
            Items.Add(rep);
        }
    }
}
