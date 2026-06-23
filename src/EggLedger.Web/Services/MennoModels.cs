using System.Text.Json.Serialization;

namespace EggLedger.Web.Services;

/// <summary>
/// Strongly-typed Menno community drop-rate DTOs. C# port of the Go menno
/// package structs (ConfigurationItem and friends), decoded with
/// System.Text.Json.
///
/// <para>This is the explicit improvement over the Go path: the Go code decoded
/// the Azure response into loose interface{} maps, so a renamed or removed field
/// silently became a zero value and the UI showed empty data with no error. Here
/// every field the comparison math reads is a typed property, and
/// <see cref="MennoDecode.Validate"/> rejects an item whose required nested
/// objects failed to bind. A schema drift therefore fails loudly rather than
/// yielding silent zeros.</para>
/// </summary>
public sealed record IdNamePair
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";
}

/// <summary>Ship-side configuration of a community drop record. Port of Go ShipConfiguration.</summary>
public sealed record ShipConfiguration
{
    [JsonPropertyName("shipType")]
    public IdNamePair? ShipType { get; init; }

    [JsonPropertyName("shipDurationType")]
    public IdNamePair? ShipDurationType { get; init; }

    [JsonPropertyName("level")]
    public int Level { get; init; }

    [JsonPropertyName("targetArtifact")]
    public IdNamePair? TargetArtifact { get; init; }
}

/// <summary>Artifact-side configuration of a community drop record. Port of Go ArtifactConfiguration.</summary>
public sealed record ArtifactConfiguration
{
    [JsonPropertyName("artifactType")]
    public IdNamePair? ArtifactType { get; init; }

    [JsonPropertyName("artifactRarity")]
    public IdNamePair? ArtifactRarity { get; init; }

    [JsonPropertyName("artifactLevel")]
    public int ArtifactLevel { get; init; }
}

/// <summary>
/// One community aggregate record: a ship+artifact configuration and the total
/// drops observed for it. Port of Go ConfigurationItem. The Azure endpoint
/// returns a raw JSON array of these.
/// </summary>
public sealed record ConfigurationItem
{
    [JsonPropertyName("shipConfiguration")]
    public ShipConfiguration? ShipConfiguration { get; init; }

    [JsonPropertyName("artifactConfiguration")]
    public ArtifactConfiguration? ArtifactConfiguration { get; init; }

    [JsonPropertyName("totalDrops")]
    public int TotalDrops { get; init; }
}
