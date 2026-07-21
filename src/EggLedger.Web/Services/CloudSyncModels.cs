using System.Text.Json.Serialization;

namespace EggLedger.Web.Services;

public sealed record AuthInitResponse(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("state")] string State);

public sealed record PollResponse(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("avatarUrl")] string AvatarUrl,
    [property: JsonPropertyName("encryptionKey")] string EncryptionKey);

public sealed record PutBlobRequest(
    [property: JsonPropertyName("ciphertext")] string Ciphertext);

public sealed record GetBlobResponse(
    [property: JsonPropertyName("ciphertext")] string Ciphertext,
    [property: JsonPropertyName("updatedAt")] long UpdatedAt);

public sealed record BlobListEntry(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("updatedAt")] long UpdatedAt);

public sealed record CloudSession(
    string Token,
    string Username,
    string AvatarUrl,
    string EncryptionKey);

public sealed record PollResult(bool Pending, CloudSession? Session) {
    public static PollResult StillPending { get; } = new(true, null);
    public static PollResult Done(CloudSession session) => new(false, session);
}

public sealed class CloudSyncException : Exception {
    public CloudSyncException(string message) : base(message) { }
    public CloudSyncException(string message, Exception inner) : base(message, inner) { }
}
