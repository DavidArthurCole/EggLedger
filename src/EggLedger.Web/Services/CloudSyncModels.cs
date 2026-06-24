using System.Text.Json.Serialization;

namespace EggLedger.Web.Services;

/// <summary>Response of <c>GET /api/v1/auth/discord</c>: the OAuth URL plus the poll state token.</summary>
public sealed record AuthInitResponse(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("state")] string State);

/// <summary>Successful body of <c>GET /api/v1/auth/poll?state=</c>. The four camelCase fields are a frozen contract; <c>EncryptionKey</c> is the per-user AES hex key.</summary>
public sealed record PollResponse(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("avatarUrl")] string AvatarUrl,
    [property: JsonPropertyName("encryptionKey")] string EncryptionKey);

/// <summary>Request body for <c>PUT /api/v1/blobs/{name}</c>: the base64 ciphertext.</summary>
public sealed record PutBlobRequest(
    [property: JsonPropertyName("ciphertext")] string Ciphertext);

/// <summary>Response body of <c>GET /api/v1/blobs/{name}</c>.</summary>
public sealed record GetBlobResponse(
    [property: JsonPropertyName("ciphertext")] string Ciphertext,
    [property: JsonPropertyName("updatedAt")] long UpdatedAt);

/// <summary>One entry of <c>GET /api/v1/blobs</c>.</summary>
public sealed record BlobListEntry(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("updatedAt")] long UpdatedAt);

/// <summary>Connected-session credentials: bearer token sent on every authed request plus the AES key for blobs.</summary>
public sealed record CloudSession(
    string Token,
    string Username,
    string AvatarUrl,
    string EncryptionKey);

/// <summary>Outcome of one <see cref="CloudSyncService.PollOnceAsync"/> call: still pending, or done with the session.</summary>
public sealed record PollResult(bool Pending, CloudSession? Session)
{
    public static PollResult StillPending { get; } = new(true, null);
    public static PollResult Done(CloudSession session) => new(false, session);
}

/// <summary>Raised when a cloud sync request fails loudly (auth expired, server error).</summary>
public sealed class CloudSyncException : Exception
{
    public CloudSyncException(string message) : base(message) { }
    public CloudSyncException(string message, Exception inner) : base(message, inner) { }
}
