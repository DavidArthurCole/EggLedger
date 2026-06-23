using EggLedger.Domain.Eiafx;

namespace EggLedger.Domain.Reports;

/// <summary>
/// Canonical <see cref="IWeightData"/> implementation backed by the eiafx
/// crafting-weight / family data (<see cref="EiafxData"/>). Replaces the A7
/// dangling seam: family-weighted reports now run for real.
///
/// <para>CraftingWeight mirrors Go reports.craftingWeight: the per-tier weight,
/// falling back to 1.0 when absent or the cycle-sentinel (0).</para>
/// </summary>
public sealed class EiafxWeightData : IWeightData
{
    /// <summary>Shared stateless instance.</summary>
    public static readonly EiafxWeightData Instance = new();

    /// <inheritdoc />
    public double CraftingWeight(long artifactId, long level) =>
        EiafxData.CraftingWeightOrOne(artifactId, level);

    /// <inheritdoc />
    public IReadOnlyList<int> FamilyAfxIds(string familyId) =>
        EiafxData.FamilyAfxIds.TryGetValue(familyId, out var ids) ? ids : Array.Empty<int>();
}
