namespace EggLedger.Domain.Reports;

/// <summary>
/// Data-access contract for the report engine. Mirrors Go's injected *sql.DB
/// (SetMissionDB). The implementation is deferred (host-coupled); the executor
/// only depends on this interface so its math stays pure and testable.
///
/// Query returns rows as positional column arrays in the order the SQL SELECTs
/// them, matching the Go rows.Scan order. Numeric columns should be boxed as
/// long (integers) or double (REAL); text columns as string.
/// </summary>
public interface IMissionDb
{
    /// <summary>
    /// Executes a parameterized SQL query and returns all rows. Each row is an
    /// array of column values positionally matching the SELECT list. Args are the
    /// ordered "?" parameter bindings.
    /// </summary>
    IReadOnlyList<object?[]> Query(string sql, IReadOnlyList<object?> args);
}

/// <summary>
/// Supplies eiafx-derived weighting data used by family-weighted reports:
/// per-(artifactId, level) crafting weights and per-family artifact id sets.
/// Mirrors Go's eiafx.CraftingWeights / eiafx.FamilyAFXIds globals. Kept behind
/// an interface because the eiafx data load is not part of this pure port.
/// </summary>
public interface IWeightData
{
    /// <summary>
    /// Per-tier crafting weight for an artifact (id, level). Returns 1 when the key
    /// is the cycle sentinel (weight 0) so the row is not dropped. Port of Go
    /// craftingWeight.
    /// </summary>
    double CraftingWeight(long artifactId, long level);

    /// <summary>
    /// All afx ids in the given family, or an empty list if unknown. Mirrors
    /// eiafx.FamilyAFXIds[familyId].
    /// </summary>
    IReadOnlyList<int> FamilyAfxIds(string familyId);
}
