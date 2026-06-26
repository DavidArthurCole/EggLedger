using System.Text.Json.Serialization;

namespace EggLedger.Domain.LedgerData;

/// <summary>
/// All game display strings extracted from source. Mirrors Go ledgerdata.LedgerDisplayData
/// struct and JSON field names exactly.
/// </summary>
public sealed class LedgerDisplayData {
    /// <summary>Maps proto enum name to a [level][rarity] string matrix of effect substitution values.</summary>
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

/// <summary>Maps an EB order-of-magnitude bucket to a display tier.</summary>
public sealed class FarmerRole {
    [JsonPropertyName("oom")]
    public int Oom { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    [JsonPropertyName("color")]
    public string Color { get; set; } = "";
}

/// <summary>A filterable mission target entry with display metadata.</summary>
public sealed class ArtifactTarget {
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";
    [JsonPropertyName("imageString")]
    public string ImageString { get; set; } = "";
}
