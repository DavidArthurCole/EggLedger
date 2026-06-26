using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EggLedger.Domain.Crypto;

namespace EggLedger.Web.Services;

/// <summary>Browser cloud-sync client: same-origin Discord OAuth (redirect + poll) plus generic encrypted blob transport. Blobs are encrypted client-side under the per-user key before PUT and decrypted after GET.</summary>
public sealed class CloudSyncService {
    /// <summary>Same-origin API prefix, relative to the HttpClient base address.</summary>
    public const string ApiPrefix = "api/v1";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly INavigation _nav;
    private readonly IBlobCipher _cipher;

    public CloudSyncService(HttpClient http, INavigation nav, IBlobCipher cipher) {
        _http = http;
        _nav = nav;
        _cipher = cipher;
    }

    /// <summary>Probes <c>GET /api/v1/verify</c> for reachability. Any non-200 or transport error returns false; never throws.</summary>
    public async Task<bool> CheckReachableAsync(CancellationToken cancellationToken = default) {
        try {
            using var resp = await _http.GetAsync($"{ApiPrefix}/verify", cancellationToken).ConfigureAwait(false);
            return resp.StatusCode == HttpStatusCode.OK;
        } catch (HttpRequestException) {
            return false;
        } catch (TaskCanceledException) {
            return false;
        }
    }

    /// <summary>Starts the Discord OAuth flow: gets the auth URL + poll state, redirects the browser, and returns the state to poll via <see cref="PollOnceAsync"/>.</summary>
    public async Task<string> BeginAuthAsync(CancellationToken cancellationToken = default) {
        AuthInitResponse? init;
        using (var resp = await _http.GetAsync($"{ApiPrefix}/auth/discord", cancellationToken).ConfigureAwait(false)) {
            if (!resp.IsSuccessStatusCode) {
                throw new CloudSyncException($"auth init: server returned {(int)resp.StatusCode}");
            }
            init = await resp.Content.ReadFromJsonAsync<AuthInitResponse>(Json, cancellationToken).ConfigureAwait(false);
        }
        if (init is null || string.IsNullOrEmpty(init.Url) || string.IsNullOrEmpty(init.State)) {
            throw new CloudSyncException("auth init: malformed response");
        }

        _nav.NavigateTo(init.Url);
        return init.State;
    }

    /// <summary>Polls <c>GET /api/v1/auth/poll?state=</c> once. 202 and 404 both mean keep polling; 200 returns the session; any other status fails loudly.</summary>
    public async Task<PollResult> PollOnceAsync(string state, CancellationToken cancellationToken = default) {
        var url = $"{ApiPrefix}/auth/poll?state={Uri.EscapeDataString(state)}";
        using var resp = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);

        switch (resp.StatusCode) {
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

    /// <summary>Invalidates the server-side session (<c>DELETE /api/v1/auth/session</c>). Server always returns 204; clearing local creds is the caller's job.</summary>
    public async Task DisconnectAsync(string token, CancellationToken cancellationToken = default) {
        using var req = new HttpRequestMessage(HttpMethod.Delete, $"{ApiPrefix}/auth/session");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        // Server always 204; errors are non-fatal, so the response is ignored.
        using var resp = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sets the bearer header, sends the request, and applies the shared 401 check. Callers own the returned response and remaining status handling.</summary>
    private async Task<HttpResponseMessage> SendAuthedAsync(
        CloudSession session, HttpRequestMessage request, string expiredMessage, CancellationToken cancellationToken) {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
        var resp = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (resp.StatusCode == HttpStatusCode.Unauthorized) {
            resp.Dispose();
            throw new CloudSyncException(expiredMessage);
        }
        return resp;
    }

    /// <summary>Serializes the payload to JSON, encrypts under the session key, and PUTs the ciphertext to <c>/api/v1/blobs/{name}</c>.</summary>
    public async Task PutBlobAsync<T>(CloudSession session, string name, T payload, CancellationToken cancellationToken = default) {
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(payload, Json);
        var ciphertext = await _cipher.EncryptAsync(session.EncryptionKey, plaintext, cancellationToken).ConfigureAwait(false);

        using var req = new HttpRequestMessage(HttpMethod.Put, $"{ApiPrefix}/blobs/{Uri.EscapeDataString(name)}");
        req.Content = JsonContent.Create(new PutBlobRequest(ciphertext), options: Json);

        using var resp = await SendAuthedAsync(session, req, $"putBlob {name}: session expired - please reconnect", cancellationToken).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) {
            throw new CloudSyncException($"putBlob {name}: server error {(int)resp.StatusCode}");
        }
    }

    /// <summary>GETs <c>/api/v1/blobs/{name}</c>, decrypts under the session key, and deserializes into <typeparamref name="T"/>. 404 and 401 both fail loudly.</summary>
    public async Task<T> GetBlobAsync<T>(CloudSession session, string name, CancellationToken cancellationToken = default) {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiPrefix}/blobs/{Uri.EscapeDataString(name)}");

        using var resp = await SendAuthedAsync(session, req, $"getBlob {name}: session expired - please reconnect", cancellationToken).ConfigureAwait(false);
        if (resp.StatusCode == HttpStatusCode.NotFound) {
            throw new CloudSyncException($"getBlob {name}: not found (nothing synced yet?)");
        }
        if (!resp.IsSuccessStatusCode) {
            throw new CloudSyncException($"getBlob {name}: server error {(int)resp.StatusCode}");
        }

        var env = await resp.Content.ReadFromJsonAsync<GetBlobResponse>(Json, cancellationToken).ConfigureAwait(false)
            ?? throw new CloudSyncException($"getBlob {name}: malformed response");
        byte[] plaintext;
        try {
            plaintext = await _cipher.DecryptAsync(session.EncryptionKey, env.Ciphertext, cancellationToken).ConfigureAwait(false);
        } catch (Exception ex) when (ex is not CloudSyncException) {
            throw new CloudSyncException($"getBlob {name}: decrypt failed: {ex.Message}", ex);
        }

        var value = JsonSerializer.Deserialize<T>(plaintext, Json) ?? throw new CloudSyncException($"getBlob {name}: decoded payload was null");
        return value;
    }

    /// <summary>Lists the caller's blob names + update times (<c>GET /api/v1/blobs</c>). Empty list when none.</summary>
    public async Task<IReadOnlyList<BlobListEntry>> ListBlobsAsync(CloudSession session, CancellationToken cancellationToken = default) {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiPrefix}/blobs");

        using var resp = await SendAuthedAsync(session, req, "listBlobs: session expired - please reconnect", cancellationToken).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) {
            throw new CloudSyncException($"listBlobs: server error {(int)resp.StatusCode}");
        }
        return await resp.Content.ReadFromJsonAsync<List<BlobListEntry>>(Json, cancellationToken).ConfigureAwait(false)
            ?? [];
    }

    /// <summary>Deletes one blob (<c>DELETE /api/v1/blobs/{name}</c>). Server returns 204.</summary>
    public async Task DeleteBlobAsync(CloudSession session, string name, CancellationToken cancellationToken = default) {
        using var req = new HttpRequestMessage(HttpMethod.Delete, $"{ApiPrefix}/blobs/{Uri.EscapeDataString(name)}");
        using var resp = await SendAuthedAsync(session, req, "deleteBlob: session expired - please reconnect", cancellationToken).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) {
            throw new CloudSyncException($"deleteBlob {name}: server error {(int)resp.StatusCode}");
        }
    }

    /// <summary>Deletes the user's entire cloud account (<c>DELETE /api/v1/user</c>, CASCADE). Server returns 204; caller clears local creds after.</summary>
    public async Task DeleteAccountAsync(CloudSession session, CancellationToken cancellationToken = default) {
        using var req = new HttpRequestMessage(HttpMethod.Delete, $"{ApiPrefix}/user");
        using var resp = await SendAuthedAsync(session, req, "deleteAccount: session expired - please reconnect", cancellationToken).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) {
            throw new CloudSyncException($"deleteAccount: server error {(int)resp.StatusCode}");
        }
    }
}
