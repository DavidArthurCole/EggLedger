using EggLedger.Domain.Eiafx;

namespace EggLedger.Domain.Reports;

/// <summary>
/// Canonical <see cref="IWeightData"/> backed by eiafx crafting-weight / family data.
/// CraftingWeight mirrors Go reports.craftingWeight, falling back to 1.0 when absent or the cycle-sentinel (0).
/// </summary>
public sealed class EiafxWeightData : IWeightData
{
    public static readonly EiafxWeightData Instance = new();

    public double CraftingWeight(long artifactId, long level) =>
        EiafxData.CraftingWeightOrOne(artifactId, level);

    public IReadOnlyList<int> FamilyAfxIds(string familyId) =>
        EiafxData.FamilyAfxIds.TryGetValue(familyId, out var ids) ? ids : Array.Empty<int>();
}
