using EggLedger.Domain.MissionQuery;

namespace EggLedger.Web.Missions;

/// <summary>
/// Aggregated lifetime drops for one account. C# port of the Vue LifetimeData
/// shape (www/src/composables/useLifetimeSorting.ts). Four spec-type drop lists
/// of grouped representatives plus the contributing mission count.
/// </summary>
public sealed class LifetimeData
{
    public int MissionCount { get; set; }
    public List<DropLike> Artifacts { get; set; } = new();
    public List<DropLike> Stones { get; set; } = new();
    public List<DropLike> StoneFragments { get; set; } = new();
    public List<DropLike> Ingredients { get; set; } = new();
}

/// <summary>
/// Pure lifetime drop aggregation. C# port of the mergeItems / bucket logic in
/// www/src/views/LifetimeDataView.vue (viewLifetimeDataOfEid). Folds every
/// mission's drops into four spec-type buckets, combining identical drops and
/// summing their counts.
///
/// CAUTION (empty-artifacts history): the read side and the write side must agree
/// on SpecType values. The Domain <see cref="MissionDrop.SpecType"/> is one of
/// "Artifact" / "Stone" / "StoneFragment" / "Ingredient" (set in
/// MissionQueryHandlers.ShapeDrop). The Vue splits on exactly those strings; this
/// port matches them verbatim so no drop is silently filtered out.
/// </summary>
public static class LifetimeAggregator
{
    /// <summary>
    /// Aggregates the per-mission drop dictionary returned by
    /// <c>GetAllPlayerDropsAsync</c> into the four spec-type buckets.
    ///
    /// Merge key mirrors the Vue mergeItems: <c>id_level_rarity</c> within a bucket.
    /// First occurrence is kept as the representative; later identical drops only
    /// bump its <see cref="DropLike.Count"/>. Each raw <see cref="MissionDrop"/>
    /// counts as 1 (one stored row per drop), matching the Vue
    /// <c>count: d.count ?? 1</c>.
    ///
    /// MissionCount counts the missions present in the dictionary (the Vue uses the
    /// id list length); pass only the missions you intend to include.
    /// </summary>
    public static LifetimeData Aggregate(IReadOnlyDictionary<string, List<MissionDrop>> dropsByMission)
    {
        var artifacts = new Bucket();
        var stones = new Bucket();
        var stoneFragments = new Bucket();
        var ingredients = new Bucket();

        foreach (var kvp in dropsByMission)
        {
            foreach (var drop in kvp.Value)
            {
                var bucket = drop.SpecType switch
                {
                    "Artifact" => artifacts,
                    "Stone" => stones,
                    "StoneFragment" => stoneFragments,
                    "Ingredient" => ingredients,
                    _ => null,
                };
                bucket?.Merge(drop);
            }
        }

        return new LifetimeData
        {
            MissionCount = dropsByMission.Count,
            Artifacts = artifacts.Items,
            Stones = stones.Items,
            StoneFragments = stoneFragments.Items,
            Ingredients = ingredients.Items,
        };
    }

    // Dedupe-on-insert bucket: an ordered list plus a key index, matching the Vue
    // LifetimeBucket (arr + index Map).
    private sealed class Bucket
    {
        public List<DropLike> Items { get; } = new();
        private readonly Dictionary<string, DropLike> _index = new();

        public void Merge(MissionDrop drop)
        {
            string key = drop.Id + "_" + drop.Level + "_" + drop.Rarity;
            if (_index.TryGetValue(key, out var existing))
            {
                existing.Count += 1;
                return;
            }
            var rep = new DropLike
            {
                Id = drop.Id,
                Name = drop.Name,
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
