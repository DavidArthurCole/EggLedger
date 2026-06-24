using EggLedger.Domain.Ei;
using Ei;

namespace EggLedger.Domain.Tests;

public class ArtifactExtensionsTests {
    [Fact]
    public void DropEffectString_KnownArtifact() {
        var spec = new ArtifactSpec {
            name = ArtifactSpec.Name.TachyonDeflector,
            level = ArtifactSpec.Level.Greater,
            rarity = ArtifactSpec.Rarity.Legendary,
        };
        Assert.Equal("[20%]", spec.DropEffectString());
    }

    [Fact]
    public void DropEffectString_UnknownArtifact() {
        var spec = new ArtifactSpec {
            name = ArtifactSpec.Name.Unknown,
            level = ArtifactSpec.Level.Inferior,
            rarity = ArtifactSpec.Rarity.Common,
        };
        Assert.Equal("", spec.DropEffectString());
    }

    [Fact]
    public void DropEffectString_NilLevel() {
        var spec = new ArtifactSpec {
            name = ArtifactSpec.Name.TachyonDeflector,
            rarity = ArtifactSpec.Rarity.Common,
        };
        Assert.Equal("", spec.DropEffectString());
    }

    [Fact]
    public void DropEffectString_NilName() {
        var spec = new ArtifactSpec {
            level = ArtifactSpec.Level.Inferior,
            rarity = ArtifactSpec.Rarity.Common,
        };
        Assert.Equal("", spec.DropEffectString());
    }

    [Fact]
    public void TierName_ArtifactT1() {
        var spec = new ArtifactSpec {
            name = ArtifactSpec.Name.TachyonDeflector,
            level = ArtifactSpec.Level.Inferior,
        };
        Assert.Equal("WEAK", spec.TierName());
    }

    [Fact]
    public void TierName_ArtifactT4() {
        var spec = new ArtifactSpec {
            name = ArtifactSpec.Name.TachyonDeflector,
            level = ArtifactSpec.Level.Greater,
        };
        Assert.Equal("EGGCEPTIONAL", spec.TierName());
    }

    [Fact]
    public void TierName_StoneFragment() {
        var spec = new ArtifactSpec {
            name = ArtifactSpec.Name.TachyonStoneFragment,
            level = ArtifactSpec.Level.Inferior,
        };
        Assert.Equal("FRAGMENT", spec.TierName());
    }

    [Fact]
    public void TierName_Ingredient() {
        var spec = new ArtifactSpec {
            name = ArtifactSpec.Name.GoldMeteorite,
            level = ArtifactSpec.Level.Normal,
        };
        Assert.Equal("SOLID", spec.TierName());
    }

    [Fact]
    public void TierName_UnknownArtifact() {
        var spec = new ArtifactSpec {
            name = ArtifactSpec.Name.Unknown,
            level = ArtifactSpec.Level.Inferior,
        };
        Assert.Equal("?", spec.TierName());
    }

    [Fact]
    public void TierName_NilLevel() {
        var spec = new ArtifactSpec { name = ArtifactSpec.Name.TachyonDeflector };
        Assert.Equal("?", spec.TierName());
    }

    [Fact]
    public void TierName_NilName() {
        var spec = new ArtifactSpec { level = ArtifactSpec.Level.Inferior };
        Assert.Equal("?", spec.TierName());
    }

    [Fact]
    public void GenericBenefitString_KnownArtifact() {
        var spec = new ArtifactSpec { name = ArtifactSpec.Name.QuantumMetronome };
        Assert.Equal("[+^b] egg laying rate", spec.GenericBenefitString());
    }

    [Fact]
    public void GenericBenefitString_Stone() {
        var spec = new ArtifactSpec { name = ArtifactSpec.Name.SoulStone };
        Assert.Equal("[+^b] bonus per Soul Egg", spec.GenericBenefitString());
    }

    [Fact]
    public void GenericBenefitString_Unknown() {
        var spec = new ArtifactSpec { name = ArtifactSpec.Name.Unknown };
        Assert.Equal("", spec.GenericBenefitString());
    }

    [Fact]
    public void GenericBenefitString_TungstenAnkhMatchesDemeter() {
        var ankh = new ArtifactSpec { name = ArtifactSpec.Name.TungstenAnkh };
        var necklace = new ArtifactSpec { name = ArtifactSpec.Name.DemetersNecklace };
        Assert.Equal(necklace.GenericBenefitString(), ankh.GenericBenefitString());
    }

    [Fact]
    public void GenericBenefitString_NilName() {
        var spec = new ArtifactSpec();
        Assert.Equal("", spec.GenericBenefitString());
    }

    [Fact]
    public void InventoryVisualizerOrder_Artifact() {
        Assert.Equal(33, ArtifactSpec.Name.BookOfBasan.InventoryVisualizerOrder());
    }

    [Fact]
    public void InventoryVisualizerOrder_Stone() {
        Assert.Equal(6, ArtifactSpec.Name.TachyonStone.InventoryVisualizerOrder());
    }

    [Fact]
    public void InventoryVisualizerOrder_StoneFragmentMatchesStone() {
        var stone = ArtifactSpec.Name.TachyonStone.InventoryVisualizerOrder();
        var frag = ArtifactSpec.Name.TachyonStoneFragment.InventoryVisualizerOrder();
        Assert.Equal(stone, frag);
    }

    [Fact]
    public void InventoryVisualizerOrder_Unknown() {
        Assert.Equal(0, ArtifactSpec.Name.Unknown.InventoryVisualizerOrder());
    }

    [Fact]
    public void ArtifactType_Artifact() {
        Assert.Equal(ArtifactSpec.Type.Artifact, ArtifactSpec.Name.BookOfBasan.ArtifactType());
    }

    [Fact]
    public void ArtifactType_Stone() {
        Assert.Equal(ArtifactSpec.Type.Stone, ArtifactSpec.Name.TachyonStone.ArtifactType());
    }

    [Fact]
    public void ArtifactType_StoneIngredient() {
        Assert.Equal(
            ArtifactSpec.Type.StoneIngredient,
            ArtifactSpec.Name.TachyonStoneFragment.ArtifactType());
    }

    [Fact]
    public void ArtifactType_Ingredient() {
        Assert.Equal(ArtifactSpec.Type.Ingredient, ArtifactSpec.Name.GoldMeteorite.ArtifactType());
    }

    [Fact]
    public void ArtifactType_Unknown() {
        Assert.Equal(ArtifactSpec.Type.Artifact, ArtifactSpec.Name.Unknown.ArtifactType());
    }

    [Fact]
    public void CorrespondingStone() {
        Assert.Equal(
            ArtifactSpec.Name.TachyonStone,
            ArtifactSpec.Name.TachyonStoneFragment.CorrespondingStone());
    }

    [Fact]
    public void CorrespondingStone_NonFragment() {
        Assert.Equal(
            ArtifactSpec.Name.Unknown,
            ArtifactSpec.Name.TachyonStone.CorrespondingStone());
    }

    [Fact]
    public void CorrespondingFragment() {
        Assert.Equal(
            ArtifactSpec.Name.TachyonStoneFragment,
            ArtifactSpec.Name.TachyonStone.CorrespondingFragment());
    }

    [Fact]
    public void CorrespondingFragment_NonStone() {
        Assert.Equal(
            ArtifactSpec.Name.Unknown,
            ArtifactSpec.Name.TachyonStoneFragment.CorrespondingFragment());
    }
}
