using System.Text.Json.Serialization;

namespace EggLedger.Domain.LedgerData;

public sealed class LedgerDisplayData {
    [JsonPropertyName("artifactEffects")]
    public Dictionary<string, string[][]> ArtifactEffects { get; set; } = [];
    [JsonPropertyName("farmerRoles")]
    public List<FarmerRole> FarmerRoles { get; set; } = [];
    [JsonPropertyName("shipNames")]
    public Dictionary<string, string> ShipNames { get; set; } = [];
    [JsonPropertyName("artifactTargets")]
    public List<ArtifactTarget> ArtifactTargets { get; set; } = [];
    [JsonPropertyName("artifactTierNames")]
    public Dictionary<string, string[]> ArtifactTierNames { get; set; } = [];
    [JsonPropertyName("inventoryVisualizerOrder")]
    public Dictionary<string, int> InventoryVisualizerOrder { get; set; } = [];
    [JsonPropertyName("genericBenefitStrings")]
    public Dictionary<string, string> GenericBenefitStrings { get; set; } = [];
    [JsonPropertyName("artifactTypes")]
    public Dictionary<string, string> ArtifactTypes { get; set; } = [];
    [JsonPropertyName("stoneFragmentMap")]
    public Dictionary<string, string> StoneFragmentMap { get; set; } = [];
}

public sealed class FarmerRole {
    [JsonPropertyName("oom")]
    public int Oom { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    [JsonPropertyName("color")]
    public string Color { get; set; } = "";
}

public sealed class ArtifactTarget {
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";
    [JsonPropertyName("imageString")]
    public string ImageString { get; set; } = "";
}
