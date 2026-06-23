using EggLedger.Domain.Eiafx;
using EggLedger.Domain.Reports;

namespace EggLedger.Domain.Tests.Eiafx;

/// <summary>
/// Golden tests for the eiafx crafting-weight / family data. Ports Go
/// eiafx/data_test.go and pins additional hand-computed values so the recursive
/// weight expansion is locked.
/// </summary>
public class EiafxDataTests
{
    [Fact]
    public void LoadDataConfig_Populates()
    {
        Assert.NotEmpty(EiafxData.Families);
        Assert.NotEmpty(EiafxData.FamilyAfxIds);
        Assert.NotEmpty(EiafxData.CraftingWeights);
    }

    // Port of Go TestCraftingWeights_BaseItem.
    // Tachyon stone fragment: afx_id=2, afx_level=0. No recipe -> weight 1.0.
    [Fact]
    public void CraftingWeights_BaseItem_IsOne()
    {
        Assert.Equal(1.0, EiafxData.CraftingWeights[(2, 0)]);
    }

    // Port of Go TestCraftingWeights_SelfContained.
    // Tachyon stone T1: afx_id=1, afx_level=0. Recipe: 20x fragment -> 20.0.
    [Fact]
    public void CraftingWeights_SelfContained_Is20()
    {
        Assert.Equal(20.0, EiafxData.CraftingWeights[(1, 0)]);
    }

    // Port of Go TestCraftingWeights_CrossFamily.
    // Puzzle Cube T3: afx_id=23, afx_level=2.
    // 7x {23,1}(weight 3) + 2x {8,0} gusset base (weight 1) = 23.
    [Fact]
    public void CraftingWeights_CrossFamily_Is23()
    {
        Assert.Equal(23.0, EiafxData.CraftingWeights[(23, 2)]);
    }

    // Hand-computed additional pins so the recursion is fully locked.
    // Puzzle cube T2: afx_id=23, afx_level=1. Recipe: 3x {23,0}(base=1) = 3.
    [Fact]
    public void CraftingWeights_PuzzleCubeT2_Is3()
    {
        Assert.Equal(3.0, EiafxData.CraftingWeights[(23, 1)]);
    }

    // Solar titanium T2: afx_id=43, afx_level=1. Recipe: 10x {43,0}(base=1) = 10.
    // T3: afx_id=43, afx_level=2. Recipe: 12x {43,1}(=10) = 120.
    [Fact]
    public void CraftingWeights_SolarTitaniumChain()
    {
        Assert.Equal(10.0, EiafxData.CraftingWeights[(43, 1)]);
        Assert.Equal(120.0, EiafxData.CraftingWeights[(43, 2)]);
    }

    // Lunar totem T3: afx_id=0, afx_level=3.
    // Recipe: 6x {0,2} + 1x {43,1}.
    //   {0,0}=1; {0,1}=3x{0,0}=3; {0,2}=6x{0,1}=18; {43,1}=10.
    //   => 6*18 + 1*10 = 118.
    [Fact]
    public void CraftingWeights_LunarTotemT3_Is118()
    {
        Assert.Equal(118.0, EiafxData.CraftingWeights[(0, 3)]);
    }

    // Port of Go TestFamilyAFXIds_TachyonStone (child_afx_ids = [1, 2]).
    [Fact]
    public void FamilyAfxIds_TachyonStone()
    {
        Assert.True(EiafxData.FamilyAfxIds.TryGetValue("tachyon-stone", out var ids));
        Assert.Equal(new[] { 1, 2 }, ids);
    }

    [Fact]
    public void Families_FirstIsPuzzleCube()
    {
        Assert.Equal("puzzle-cube", EiafxData.Families[0].Id);
        Assert.Equal("Puzzle cube", EiafxData.Families[0].Name);
    }

    // EiafxWeightData adapter backs Reports: absent / sentinel keys fall back to 1.
    [Fact]
    public void EiafxWeightData_CraftingWeight_FallsBackToOne()
    {
        var wd = EiafxWeightData.Instance;
        Assert.Equal(20.0, wd.CraftingWeight(1, 0));
        Assert.Equal(1.0, wd.CraftingWeight(2, 0));
        // Unknown key -> 1.0 (Go craftingWeight fallback).
        Assert.Equal(1.0, wd.CraftingWeight(9999, 9));
    }

    [Fact]
    public void EiafxWeightData_FamilyAfxIds_KnownAndUnknown()
    {
        var wd = EiafxWeightData.Instance;
        Assert.Equal(new[] { 1, 2 }, wd.FamilyAfxIds("tachyon-stone"));
        Assert.Empty(wd.FamilyAfxIds("does-not-exist"));
    }
}
