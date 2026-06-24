using System.Text.Json.Serialization;

namespace EggLedger.Web.Server.Sync.Blobs;

public sealed record PutBlobRequest(
    [property: JsonPropertyName("ciphertext")] string Ciphertext);

public sealed record GetBlobResponse(
    [property: JsonPropertyName("ciphertext")] string Ciphertext,
    [property: JsonPropertyName("updatedAt")] long UpdatedAt);

public sealed record BlobListEntry(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("updatedAt")] long UpdatedAt);
