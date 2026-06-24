using System.Text.Json.Serialization;

namespace EggLedger.Web.Services;

/// <summary>Strongly-typed Menno community drop-rate DTOs. Every field the comparison math reads is typed and <see cref="MennoDecode.Validate"/> rejects unbound required nested objects, so a schema drift fails loudly instead of yielding silent zeros.</summary>
public sealed record IdNamePair {
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";
}

/// <summary>Ship-side configuration of a community drop record.</summary>
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

/// <summary>Artifact-side configuration of a community drop record.</summary>
public sealed record ArtifactConfiguration {
    [JsonPropertyName("artifactType")]
    public IdNamePair? ArtifactType { get; init; }

    [JsonPropertyName("artifactRarity")]
    public IdNamePair? ArtifactRarity { get; init; }

    [JsonPropertyName("artifactLevel")]
    public int ArtifactLevel { get; init; }
}

/// <summary>One community aggregate record: a ship+artifact configuration and its total drops. The endpoint returns a raw JSON array of these.</summary>
public sealed record ConfigurationItem {
    [JsonPropertyName("shipConfiguration")]
    public ShipConfiguration? ShipConfiguration { get; init; }

    [JsonPropertyName("artifactConfiguration")]
    public ArtifactConfiguration? ArtifactConfiguration { get; init; }

    [JsonPropertyName("totalDrops")]
    public int TotalDrops { get; init; }
}
