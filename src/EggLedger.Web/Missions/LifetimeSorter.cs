namespace EggLedger.Web.Missions;

/// <summary>Lifetime drop sort method. Port of the Vue lifetimeSortMethod union.</summary>
public enum LifetimeSortMethod
{
    Default,
    Iv,
    Count,
    Random,
}

/// <summary>
/// Sort/ordering for aggregated lifetime drops. C# port of the sort functions in
/// www/src/composables/useLifetimeSorting.ts. Reuses <see cref="DropSorter"/> for
/// the shared comparators (default = sortGroupAlreadyCombed, iv =
/// inventoryVisualizerSort) and adds the count comparator and the random shuffle
/// that are unique to the Lifetime tab.
/// </summary>
public static class LifetimeSorter
{
    /// <summary>
    /// Parses the persisted sort-method string ("default"/"iv"/"count"/"random").
    /// Unknown values fall back to <see cref="LifetimeSortMethod.Default"/>,
    /// matching the Vue switch default.
    /// </summary>
    public static LifetimeSortMethod ParseMethod(string? value) => value switch
    {
        "iv" => LifetimeSortMethod.Iv,
        "count" => LifetimeSortMethod.Count,
        "random" => LifetimeSortMethod.Random,
        _ => LifetimeSortMethod.Default,
    };

    /// <summary>Serializes a method back to the persisted string.</summary>
    public static string MethodString(LifetimeSortMethod method) => method switch
    {
        LifetimeSortMethod.Iv => "iv",
        LifetimeSortMethod.Count => "count",
        LifetimeSortMethod.Random => "random",
        _ => "default",
    };

    /// <summary>
    /// Re-sorts every spec-type list in place per the active method, mirroring the
    /// Vue reSortLifetime (which rebuilds each list with the chosen sortFn). Random
    /// uses the supplied RNG so callers can seed it for tests.
    /// </summary>
    public static void Sort(LifetimeData data, LifetimeSortMethod method, Random? rng = null)
    {
        data.Artifacts = SortList(data.Artifacts, method, rng);
        data.Stones = SortList(data.Stones, method, rng);
        data.StoneFragments = SortList(data.StoneFragments, method, rng);
        data.Ingredients = SortList(data.Ingredients, method, rng);
    }

    private static List<DropLike> SortList(IReadOnlyList<DropLike> list, LifetimeSortMethod method, Random? rng) => method switch
    {
        LifetimeSortMethod.Iv => DropSorter.InventoryVisualizerSort(list),
        LifetimeSortMethod.Count => SortGroupByCount(list),
        LifetimeSortMethod.Random => Shuffle(list, rng ?? Random.Shared),
        _ => DropSorter.SortGroupAlreadyCombed(list),
    };

    /// <summary>
    /// Port of sortGroupByCount: count desc, then level desc, rarity desc, id desc,
    /// quality asc. No reverse (unlike sortGroupAlreadyCombed).
    /// </summary>
    public static List<DropLike> SortGroupByCount(IEnumerable<DropLike> collection)
    {
        var indexed = new List<(DropLike Item, int Index)>();
        int i = 0;
        foreach (var item in collection)
        {
            indexed.Add((item, i++));
        }
        indexed.Sort((a, b) =>
        {
            int c = CountComparer(a.Item, b.Item);
            return c != 0 ? c : a.Index.CompareTo(b.Index);
        });
        var result = new List<DropLike>(indexed.Count);
        foreach (var (item, _) in indexed)
        {
            result.Add(item);
        }
        return result;
    }

    private static int CountComparer(DropLike a, DropLike b)
    {
        if (a.Count != b.Count)
        {
            return a.Count > b.Count ? -1 : 1;
        }
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

    /// <summary>Fisher-Yates shuffle. Port of the Vue shuffle (random ordering).</summary>
    public static List<DropLike> Shuffle(IEnumerable<DropLike> collection, Random rng)
    {
        var list = new List<DropLike>(collection);
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }
}
