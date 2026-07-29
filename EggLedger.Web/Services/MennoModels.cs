using System.Text.Json.Serialization;

namespace EggLedger.Web.Services;

public sealed record IdNamePair {
    [JsonPropertyName("id")]
    public int Id { get; init; }
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";
}

public sealed record ShipConfiguration {
    [JsonPropertyName("shipType")]
    public IdNamePair? ShipType { get; init; }
    [JsonPropertyName("shipDurationType")]
    public IdNamePair? ShipDurationType { get; init; }
    [JsonPropertyName("level")]
    public int Level { get; init; }
    [JsonPropertyName("targetArtifact")]
    public IdNamePair? TargetArtifact { get; init; }
}

public sealed record ArtifactConfiguration {
    [JsonPropertyName("artifactType")]
    public IdNamePair? ArtifactType { get; init; }
    [JsonPropertyName("artifactRarity")]
    public IdNamePair? ArtifactRarity { get; init; }
    [JsonPropertyName("artifactLevel")]
    public int ArtifactLevel { get; init; }
}

public sealed record ConfigurationItem {
    [JsonPropertyName("shipConfiguration")]
    public ShipConfiguration? ShipConfiguration { get; init; }
    [JsonPropertyName("artifactConfiguration")]
    public ArtifactConfiguration? ArtifactConfiguration { get; init; }
    [JsonPropertyName("totalDrops")]
    public int TotalDrops { get; init; }
}
