using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EggLedger.Domain.Crypto;

namespace EggLedger.Web.Services;

/// <summary>
/// Browser cloud-sync client. C# port of the Go cloudsync package (auth.go +
/// blob.go), adapted from the desktop local-callback OAuth model to the
/// same-origin browser redirect + poll model.
///
/// <para>Auth: <c>GET /api/v1/auth/discord</c> returns the Discord URL and a poll
/// state token; the browser is redirected to that URL (via <see cref="INavigation"/>),
/// and the caller polls <c>GET /api/v1/auth/poll?state=</c> until it returns the
/// session. Blobs: ciphertext is encrypted client-side with
/// <see cref="BlobCrypto"/> under the per-user encryption key before PUT and
/// decrypted after GET. Authed requests carry <c>Authorization: Bearer &lt;token&gt;</c>,
/// matching the server <c>RequireAuth</c> contract.</para>
///
/// <para>Scope: the auth start/poll/session flow plus generic encrypted blob
/// transport (put/get/list/delete) and account deletion. The typed sync payloads
/// (accounts/settings/reports) and the sync UI are C3.5's concern; callers supply
/// the blob name and a serializable payload.</para>
/// </summary>
public sealed class CloudSyncService
{
    /// <summary>Same-origin API prefix. All requests are relative to the HttpClient base address.</summary>
    public const string ApiPrefix = "api/v1";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly INavigation _nav;
    private readonly IBlobCipher _cipher;

    public CloudSyncService(HttpClient http, INavigation nav, IBlobCipher cipher)
    {
        _http = http;
        _nav = nav;
        _cipher = cipher;
    }

    /// <summary>
    /// Probes <c>GET /api/v1/verify</c> with a short timeout to check the sync
    /// service is reachable. Port of the Go checkCloudSyncReachable: any non-200 or
    /// transport error is treated as unreachable (returns false, never throws).
    /// </summary>
    public async Task<bool> CheckReachableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var resp = await _http.GetAsync($"{ApiPrefix}/verify", cancellationToken).ConfigureAwait(false);
            return resp.StatusCode == HttpStatusCode.OK;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// Starts the Discord OAuth flow: asks the server for the auth URL + poll
    /// state, redirects the browser to the URL, and returns the state to poll.
    /// Port of the Go ConnectDiscord init half (the browser nav replaces
    /// open.Start; the goroutine poll loop is the caller's job via
    /// <see cref="PollOnceAsync"/>).
    /// </summary>
    public async Task<string> BeginAuthAsync(CancellationToken cancellationToken = default)
    {
        AuthInitResponse? init;
        using (var resp = await _http.GetAsync($"{ApiPrefix}/auth/discord", cancellationToken).ConfigureAwait(false))
        {
            if (!resp.IsSuccessStatusCode)
            {
                throw new CloudSyncException($"auth init: server returned {(int)resp.StatusCode}");
            }
            init = await resp.Content.ReadFromJsonAsync<AuthInitResponse>(Json, cancellationToken).ConfigureAwait(false);
        }
        if (init is null || string.IsNullOrEmpty(init.Url) || string.IsNullOrEmpty(init.State))
        {
            throw new CloudSyncException("auth init: malformed response");
        }

        _nav.NavigateTo(init.Url);
        return init.State;
    }

    /// <summary>
    /// Polls <c>GET /api/v1/auth/poll?state=</c> once. Mirrors the Go poll state
    /// machine: 202 (pending) and 404 (not yet stored) both mean keep polling;
    /// 200 returns the session; any other status fails loudly.
    /// </summary>
    public async Task<PollResult> PollOnceAsync(string state, CancellationToken cancellationToken = default)
    {
        var url = $"{ApiPrefix}/auth/poll?state={Uri.EscapeDataString(state)}";
        using var resp = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);

        switch (resp.StatusCode)
        {
            case HttpStatusCode.Accepted:
            case HttpStatusCode.NotFound:
                return PollResult.StillPending;
            case HttpStatusCode.OK:
                break;
            default:
                throw new CloudSyncException($"auth poll: unexpected status {(int)resp.StatusCode}");
        }

        var poll = await resp.Content.ReadFromJsonAsync<PollResponse>(Json, cancellationToken).ConfigureAwait(false)
            ?? throw new CloudSyncException("auth poll: malformed success response");
        var session = new CloudSession(poll.Token, poll.Username, poll.AvatarUrl, poll.EncryptionKey);
        return PollResult.Done(session);
    }

    /// <summary>
    /// Invalidates the server-side session (<c>DELETE /api/v1/auth/session</c>).
    /// Fire-and-forget on the server side; the server always returns 204. Port of
    /// the Go DisconnectCloud server half (clearing local creds is the caller's job).
    /// </summary>
    public async Task DisconnectAsync(string token, CancellationToken cancellationToken = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Delete, $"{ApiPrefix}/auth/session");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resp = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
        // Server always 204; nothing to assert. Errors are non-fatal (match Go).
    }

    /// <summary>
    /// Sets the bearer header on <paramref name="request"/>, sends it, and applies
    /// the shared 401 check (session expired - please reconnect). Callers own the
    /// returned response (disposal) and any remaining status-code handling.
    /// </summary>
    private async Task<HttpResponseMessage> SendAuthedAsync(
        CloudSession session, HttpRequestMessage request, string expiredMessage, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
        var resp = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (resp.StatusCode == HttpStatusCode.Unauthorized)
        {
            resp.Dispose();
            throw new CloudSyncException(expiredMessage);
        }
        return resp;
    }

    /// <summary>
    /// Serializes <paramref name="payload"/> to JSON, encrypts it with
    /// <see cref="BlobCrypto"/> under <paramref name="session"/>'s key, and PUTs
    /// the ciphertext to <c>/api/v1/blobs/{name}</c>. Port of Go putBlob.
    /// </summary>
    public async Task PutBlobAsync<T>(CloudSession session, string name, T payload, CancellationToken cancellationToken = default)
    {
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(payload, Json);
        var ciphertext = await _cipher.EncryptAsync(session.EncryptionKey, plaintext, cancellationToken).ConfigureAwait(false);

        using var req = new HttpRequestMessage(HttpMethod.Put, $"{ApiPrefix}/blobs/{Uri.EscapeDataString(name)}");
        req.Content = JsonContent.Create(new PutBlobRequest(ciphertext), options: Json);

        using var resp = await SendAuthedAsync(session, req, $"putBlob {name}: session expired - please reconnect", cancellationToken).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            throw new CloudSyncException($"putBlob {name}: server error {(int)resp.StatusCode}");
        }
    }

    /// <summary>
    /// GETs <c>/api/v1/blobs/{name}</c>, decrypts the ciphertext under
    /// <paramref name="session"/>'s key, and deserializes the plaintext JSON into
    /// <typeparamref name="T"/>. Port of Go getBlob. A 404 (nothing synced yet)
    /// and 401 (expired) fail loudly, matching Go.
    /// </summary>
    public async Task<T> GetBlobAsync<T>(CloudSession session, string name, CancellationToken cancellationToken = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiPrefix}/blobs/{Uri.EscapeDataString(name)}");

        using var resp = await SendAuthedAsync(session, req, $"getBlob {name}: session expired - please reconnect", cancellationToken).ConfigureAwait(false);
        if (resp.StatusCode == HttpStatusCode.NotFound)
        {
            throw new CloudSyncException($"getBlob {name}: not found (nothing synced yet?)");
        }
        if (!resp.IsSuccessStatusCode)
        {
            throw new CloudSyncException($"getBlob {name}: server error {(int)resp.StatusCode}");
        }

        var env = await resp.Content.ReadFromJsonAsync<GetBlobResponse>(Json, cancellationToken).ConfigureAwait(false)
            ?? throw new CloudSyncException($"getBlob {name}: malformed response");
        var plaintext = await _cipher.DecryptAsync(session.EncryptionKey, env.Ciphertext, cancellationToken).ConfigureAwait(false);

        var value = JsonSerializer.Deserialize<T>(plaintext, Json);
        if (value is null)
        {
            throw new CloudSyncException($"getBlob {name}: decoded payload was null");
        }
        return value;
    }

    /// <summary>Lists the caller's blob names + update times (<c>GET /api/v1/blobs</c>). Empty list when none.</summary>
    public async Task<IReadOnlyList<BlobListEntry>> ListBlobsAsync(CloudSession session, CancellationToken cancellationToken = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiPrefix}/blobs");

        using var resp = await SendAuthedAsync(session, req, "listBlobs: session expired - please reconnect", cancellationToken).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            throw new CloudSyncException($"listBlobs: server error {(int)resp.StatusCode}");
        }
        return await resp.Content.ReadFromJsonAsync<List<BlobListEntry>>(Json, cancellationToken).ConfigureAwait(false)
            ?? new List<BlobListEntry>();
    }

    /// <summary>Deletes one blob (<c>DELETE /api/v1/blobs/{name}</c>). Server returns 204.</summary>
    public async Task DeleteBlobAsync(CloudSession session, string name, CancellationToken cancellationToken = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Delete, $"{ApiPrefix}/blobs/{Uri.EscapeDataString(name)}");
        using var resp = await SendAuthedAsync(session, req, "deleteBlob: session expired - please reconnect", cancellationToken).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            throw new CloudSyncException($"deleteBlob {name}: server error {(int)resp.StatusCode}");
        }
    }

    /// <summary>
    /// Deletes the user's entire cloud account (<c>DELETE /api/v1/user</c>,
    /// CASCADE). The server returns 204; the caller clears local creds after.
    /// </summary>
    public async Task DeleteAccountAsync(CloudSession session, CancellationToken cancellationToken = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Delete, $"{ApiPrefix}/user");
        using var resp = await SendAuthedAsync(session, req, "deleteAccount: session expired - please reconnect", cancellationToken).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            throw new CloudSyncException($"deleteAccount: server error {(int)resp.StatusCode}");
        }
    }
}
