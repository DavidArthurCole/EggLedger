using System.Text.Json.Serialization;

namespace EggLedger.Domain.LedgerData;

/// <summary>
/// All game display strings extracted from source. Mirrors the Go
/// ledgerdata.LedgerDisplayData struct and JSON field names exactly.
/// </summary>
public sealed class LedgerDisplayData
{
    /// <summary>
    /// Maps proto enum name (e.g. "LUNAR_TOTEM") to a [level][rarity] string
    /// matrix of effect substitution values.
    /// </summary>
    [JsonPropertyName("artifactEffects")]
    public Dictionary<string, string[][]> ArtifactEffects { get; set; } = new();

    [JsonPropertyName("farmerRoles")]
    public List<FarmerRole> FarmerRoles { get; set; } = new();

    [JsonPropertyName("shipNames")]
    public Dictionary<string, string> ShipNames { get; set; } = new();

    [JsonPropertyName("artifactTargets")]
    public List<ArtifactTarget> ArtifactTargets { get; set; } = new();

    [JsonPropertyName("artifactTierNames")]
    public Dictionary<string, string[]> ArtifactTierNames { get; set; } = new();

    [JsonPropertyName("inventoryVisualizerOrder")]
    public Dictionary<string, int> InventoryVisualizerOrder { get; set; } = new();

    [JsonPropertyName("genericBenefitStrings")]
    public Dictionary<string, string> GenericBenefitStrings { get; set; } = new();

    [JsonPropertyName("artifactTypes")]
    public Dictionary<string, string> ArtifactTypes { get; set; } = new();

    [JsonPropertyName("stoneFragmentMap")]
    public Dictionary<string, string> StoneFragmentMap { get; set; } = new();
}

/// <summary>Maps an EB order-of-magnitude bucket to a display tier.</summary>
public sealed class FarmerRole
{
    [JsonPropertyName("oom")]
    public int Oom { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("color")]
    public string Color { get; set; } = "";
}

/// <summary>A filterable mission target entry with display metadata.</summary>
public sealed class ArtifactTarget
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("imageString")]
    public string ImageString { get; set; } = "";
}
