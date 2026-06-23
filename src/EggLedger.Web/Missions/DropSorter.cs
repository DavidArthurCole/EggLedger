namespace EggLedger.Web.Missions;

/// <summary>
/// Minimal drop shape the sort/group helpers operate on. C# port of the Vue
/// <c>DropLike</c> in useMissionSorting.ts. <see cref="Count"/> is populated by
/// <see cref="DropSorter.GroupedSpecType"/>.
/// </summary>
public sealed class DropLike
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Level { get; set; }
    public int Rarity { get; set; }
    public double Quality { get; set; }
    public int IvOrder { get; set; }
    public string SpecType { get; set; } = "";
    public int Count { get; set; }
}

/// <summary>
/// Drop combining + sorting for the mission-detail overlay. C# port of
/// www/src/composables/useMissionSorting.ts. Comparators are golden-matched to
/// the Vue source (field precedence and the trailing <c>.reverse()</c> in
/// sortGroupAlreadyCombed).
/// </summary>
public static class DropSorter
{
    /// <summary>
    /// Combines identical drops, counting duplicates. Port of groupedSpecType:
    /// key is <c>name_level_specType_rarity</c>, the first occurrence is kept and
    /// its Count incremented. Returns the grouped representatives in first-seen
    /// order.
    /// </summary>
    public static List<DropLike> GroupedSpecType(IEnumerable<DropLike> collection)
    {
        var map = new Dictionary<string, DropLike>();
        var order = new List<string>();
        foreach (var obj in collection)
        {
            string key = obj.Name + "_" + obj.Level + "_" + obj.SpecType + "_" + obj.Rarity;
            if (map.TryGetValue(key, out var existing))
            {
                existing.Count++;
            }
            else
            {
                obj.Count = 1;
                map[key] = obj;
                order.Add(key);
            }
        }
        var result = new List<DropLike>(order.Count);
        foreach (var key in order)
        {
            result.Add(map[key]);
        }
        return result;
    }

    /// <summary>
    /// Port of sortGroupAlreadyCombed. Sorts by level desc, rarity desc, id desc,
    /// quality asc, then reverses the whole list (the Vue source's
    /// <c>.sort(...).reverse()</c>), producing level asc, rarity asc, id asc,
    /// quality desc.
    /// </summary>
    public static List<DropLike> SortGroupAlreadyCombed(IEnumerable<DropLike> collection)
    {
        var list = StableSort(collection, CombedComparer);
        list.Reverse();
        return list;
    }

    /// <summary>Port of sortedGroupedSpecType: group then sortGroupAlreadyCombed.</summary>
    public static List<DropLike> SortedGroupedSpecType(IEnumerable<DropLike> collection) =>
        SortGroupAlreadyCombed(GroupedSpecType(collection));

    /// <summary>
    /// Port of inventoryVisualizerSort: rarity desc, ivOrder desc, level desc.
    /// No reverse.
    /// </summary>
    public static List<DropLike> InventoryVisualizerSort(IEnumerable<DropLike> collection)
    {
        return StableSort(collection, IvComparer);
    }

    // JS Array.prototype.sort is stable; List.Sort is not. Sort by the comparator
    // with an index tie-break so equal elements keep their original order.
    private static List<DropLike> StableSort(IEnumerable<DropLike> collection, Comparison<DropLike> cmp)
    {
        var indexed = new List<(DropLike Item, int Index)>();
        int i = 0;
        foreach (var item in collection)
        {
            indexed.Add((item, i++));
        }
        indexed.Sort((a, b) =>
        {
            int c = cmp(a.Item, b.Item);
            return c != 0 ? c : a.Index.CompareTo(b.Index);
        });
        var result = new List<DropLike>(indexed.Count);
        foreach (var (item, _) in indexed)
        {
            result.Add(item);
        }
        return result;
    }

    private static int CombedComparer(DropLike a, DropLike b)
    {
        if (a.Level != b.Level)
        {
            return a.Level > b.Level ? -1 : 1;
        }
        if (a.Rarity != b.Rarity)
        {
            return a.Rarity > b.Rarity ? -1 : 1;
        }
        if (a.Id != b.Id)
        {
            return a.Id > b.Id ? -1 : 1;
        }
        if (a.Quality != b.Quality)
        {
            return a.Quality < b.Quality ? -1 : 1;
        }
        return 0;
    }

    private static int IvComparer(DropLike a, DropLike b)
    {
        if (a.Rarity != b.Rarity)
        {
            return a.Rarity > b.Rarity ? -1 : 1;
        }
        if (a.IvOrder != b.IvOrder)
        {
            return a.IvOrder > b.IvOrder ? -1 : 1;
        }
        if (a.Level != b.Level)
        {
            return a.Level > b.Level ? -1 : 1;
        }
        return 0;
    }
}
