namespace EggLedger.Domain.Reports;

/// <summary>
/// Data-access contract for the report engine. Mirrors Go's injected *sql.DB.
/// Query returns rows as positional column arrays in SELECT order (Go rows.Scan order);
/// numeric columns boxed as long or double, text as string.
/// </summary>
public interface IMissionDb {
    /// <summary>
    /// Executes a parameterized query, returning rows as column arrays matching the
    /// SELECT list. Args are the ordered "?" parameter bindings.
    /// </summary>
    IReadOnlyList<object?[]> Query(string sql, IReadOnlyList<object?> args);
}

/// <summary>
/// Supplies eiafx-derived weighting data for family-weighted reports: per-(artifactId, level)
/// crafting weights and per-family artifact id sets. Mirrors Go eiafx.CraftingWeights / FamilyAFXIds.
/// </summary>
public interface IWeightData {
    /// <summary>
    /// Per-tier crafting weight for an artifact (id, level). Returns 1 for the cycle
    /// sentinel (weight 0) so the row is not dropped. Port of Go craftingWeight.
    /// </summary>
    double CraftingWeight(long artifactId, long level);

    /// <summary>All afx ids in the given family, empty if unknown. Mirrors eiafx.FamilyAFXIds[familyId].</summary>
    IReadOnlyList<int> FamilyAfxIds(string familyId);
}
