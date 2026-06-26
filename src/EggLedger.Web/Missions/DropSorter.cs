namespace EggLedger.Web.Missions;

/// <summary>Minimal drop shape the sort/group helpers operate on. Count is populated by GroupedSpecType.</summary>
public sealed class DropLike {
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string GameName { get; set; } = "";
    public string EffectString { get; set; } = "";
    public int Level { get; set; }
    public int Rarity { get; set; }
    public double Quality { get; set; }
    public int IvOrder { get; set; }
    public string SpecType { get; set; } = "";
    public int Count { get; set; }
}

/// <summary>Drop combining + sorting for the mission-detail overlay. Comparators golden-matched to useMissionSorting.ts (field precedence, trailing .reverse() in sortGroupAlreadyCombed).</summary>
public static class DropSorter {
    /// <summary>Combines identical drops, counting duplicates. Key is name_level_specType_rarity; representatives in first-seen order.</summary>
    public static List<DropLike> GroupedSpecType(IEnumerable<DropLike> collection) {
        var map = new Dictionary<string, DropLike>();
        var order = new List<string>();
        foreach (var obj in collection) {
            string key = obj.Name + "_" + obj.Level + "_" + obj.SpecType + "_" + obj.Rarity;
            if (map.TryGetValue(key, out var existing)) {
                existing.Count++;
            } else {
                obj.Count = 1;
                map[key] = obj;
                order.Add(key);
            }
        }
        var result = new List<DropLike>(order.Count);
        foreach (var key in order) {
            result.Add(map[key]);
        }
        return result;
    }

    /// <summary>Sorts by level/rarity/id desc, quality asc, then reverses the whole list (matching the Vue .sort(...).reverse()).</summary>
    public static List<DropLike> SortGroupAlreadyCombed(IEnumerable<DropLike> collection) {
        var list = StableSort(collection, CombedComparer);
        list.Reverse();
        return list;
    }

    public static List<DropLike> SortedGroupedSpecType(IEnumerable<DropLike> collection) =>
        SortGroupAlreadyCombed(GroupedSpecType(collection));

    /// <summary>Sorts rarity desc, ivOrder desc, level desc. No reverse.</summary>
    public static List<DropLike> InventoryVisualizerSort(IEnumerable<DropLike> collection) {
        return StableSort(collection, IvComparer);
    }

    // JS Array.prototype.sort is stable; List.Sort is not. Index tie-break keeps equal elements in original order.
    private static List<DropLike> StableSort(IEnumerable<DropLike> collection, Comparison<DropLike> cmp) {
        var indexed = new List<(DropLike Item, int Index)>();
        int i = 0;
        foreach (var item in collection) {
            indexed.Add((item, i++));
        }
        indexed.Sort((a, b) => {
            int c = cmp(a.Item, b.Item);
            return c != 0 ? c : a.Index.CompareTo(b.Index);
        });
        var result = new List<DropLike>(indexed.Count);
        foreach (var (item, _) in indexed) {
            result.Add(item);
        }
        return result;
    }

    private static int CombedComparer(DropLike a, DropLike b) {
        if (a.Level != b.Level) {
            return a.Level > b.Level ? -1 : 1;
        }
        if (a.Rarity != b.Rarity) {
            return a.Rarity > b.Rarity ? -1 : 1;
        }
        if (a.Id != b.Id) {
            return a.Id > b.Id ? -1 : 1;
        }
        if (a.Quality != b.Quality) {
            return a.Quality < b.Quality ? -1 : 1;
        }
        return 0;
    }

    private static int IvComparer(DropLike a, DropLike b) {
        if (a.Rarity != b.Rarity) {
            return a.Rarity > b.Rarity ? -1 : 1;
        }
        if (a.IvOrder != b.IvOrder) {
            return a.IvOrder > b.IvOrder ? -1 : 1;
        }
        if (a.Level != b.Level) {
            return a.Level > b.Level ? -1 : 1;
        }
        return 0;
    }
}
