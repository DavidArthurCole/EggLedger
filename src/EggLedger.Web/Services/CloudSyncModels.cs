using System.Text.Json.Serialization;

namespace EggLedger.Web.Services;

/// <summary>
/// Response of <c>GET /api/v1/auth/discord</c>: the Discord OAuth URL to send the
/// browser to plus the poll state token. Mirrors the Go authInitResponse and the
/// server DiscordInitResponse.
/// </summary>
public sealed record AuthInitResponse(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("state")] string State);

/// <summary>
/// Successful body of <c>GET /api/v1/auth/poll?state=</c>. The four camelCase
/// fields are the frozen contract (server PollResponse, Go pollResponse). The
/// <c>EncryptionKey</c> is the per-user AES hex key used for blob encrypt/decrypt.
/// </summary>
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

/// <summary>
/// The connected-session credentials returned by a completed auth poll: the
/// bearer token sent on every authed request and the AES key used to
/// encrypt/decrypt blobs. C3.5 (Settings) owns persisting these.
/// </summary>
public sealed record CloudSession(
    string Token,
    string Username,
    string AvatarUrl,
    string EncryptionKey);

/// <summary>
/// Outcome of one <see cref="CloudSyncService.PollOnceAsync"/> call: the auth
/// poll state machine. Either still pending, or done with the session.
/// </summary>
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
