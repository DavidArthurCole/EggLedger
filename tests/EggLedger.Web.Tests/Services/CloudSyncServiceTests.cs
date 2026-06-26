using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EggLedger.Domain.Crypto;
using EggLedger.Web.Services;

namespace EggLedger.Web.Tests.Services;

public sealed class CloudSyncServiceTests {
    // 64-char hex = 32-byte AES-256 key, the per-user encryptionKey shape.
    private const string HexKey = "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f";
    private const string Token = "session-token-abc";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly Uri Origin = new("https://ledgersync.test");

    private sealed record KnownAccount(string Id, string Name);

    private static CloudSession Session() => new(Token, "user#1", "https://cdn/avatar.png", HexKey);

    // Records the last URL it was sent to; never actually navigates.
    private sealed class FakeNavigation : INavigation {
        public string? LastUrl { get; private set; }
        public void NavigateTo(string url) => LastUrl = url;
    }

    // A minimal in-memory stand-in for the frozen server: blob store keyed by
    // name, the auth start/poll endpoints, and the authed-route bearer check.
    private sealed class FakeServer : HttpMessageHandler {
        private readonly Dictionary<string, string> _blobs = [];

        public string? PendingState;
        // null => still pending (202).
        public PollResponse? PollPayload;
        public string ExpectedBearer = Token;
        // Force 401 on authed routes.
        public bool RejectAuth;
        public int PutHits;
        public int GetHits;

        public IReadOnlyDictionary<string, string> Blobs => _blobs;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) {
            var path = request.RequestUri!.AbsolutePath;
            var method = request.Method;

            // Public: auth start.
            if (method == HttpMethod.Get && path == "/api/v1/auth/discord") {
                PendingState = "state-xyz";
                return Json200(new AuthInitResponse("https://discord/oauth?state=state-xyz", PendingState));
            }

            // Public: auth poll.
            if (method == HttpMethod.Get && path == "/api/v1/auth/poll") {
                if (PollPayload is null) {
                    return new HttpResponseMessage(HttpStatusCode.Accepted);
                }
                return Json200(PollPayload);
            }

            // Public: logout.
            if (method == HttpMethod.Delete && path == "/api/v1/auth/session") {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            // Everything below is authed: enforce the bearer the way RequireAuth does.
            var auth = request.Headers.Authorization;
            if (RejectAuth || auth is null || auth.Scheme != "Bearer" || auth.Parameter != ExpectedBearer) {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            if (path == "/api/v1/blobs") {
                var list = _blobs.Keys.Select(k => new BlobListEntry(k, 123)).ToList();
                return Json200(list);
            }

            if (path.StartsWith("/api/v1/blobs/", StringComparison.Ordinal)) {
                var name = Uri.UnescapeDataString(path["/api/v1/blobs/".Length..]);
                if (method == HttpMethod.Put) {
                    PutHits++;
                    var body = await request.Content!.ReadFromJsonAsync<PutBlobRequest>(Json, cancellationToken);
                    _blobs[name] = body!.Ciphertext;
                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                }
                if (method == HttpMethod.Get) {
                    GetHits++;
                    if (!_blobs.TryGetValue(name, out var ct)) {
                        return new HttpResponseMessage(HttpStatusCode.NotFound);
                    }
                    return Json200(new GetBlobResponse(ct, 123));
                }
                if (method == HttpMethod.Delete) {
                    _blobs.Remove(name);
                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                }
            }

            if (method == HttpMethod.Delete && path == "/api/v1/user") {
                _blobs.Clear();
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage Json200<T>(T value) => new(HttpStatusCode.OK) {
            Content = JsonContent.Create(value, options: Json),
        };
    }

    private static CloudSyncService Make(HttpMessageHandler server, INavigation? nav = null) {
        var http = new HttpClient(server) { BaseAddress = Origin };
        return new CloudSyncService(http, nav ?? new FakeNavigation(), new LocalBlobCipher());
    }

    [Fact]
    public async Task PutThenGet_RoundTripsThroughBlobCrypto() {
        var server = new FakeServer();
        var svc = Make(server);
        var session = Session();
        var accounts = new[] { new KnownAccount("EI1", "Alice"), new KnownAccount("EI2", "Bob") };

        await svc.PutBlobAsync(session, "accounts", accounts);

        // Server stored ciphertext, not plaintext: the stored value must decrypt
        // to the original JSON and must not contain the plaintext field names.
        var stored = server.Blobs["accounts"];
        Assert.DoesNotContain("Alice", stored, StringComparison.Ordinal);
        var decrypted = System.Text.Encoding.UTF8.GetString(BlobCrypto.Decrypt(HexKey, stored));
        Assert.Contains("Alice", decrypted, StringComparison.Ordinal);

        var roundTripped = await svc.GetBlobAsync<KnownAccount[]>(session, "accounts");
        Assert.Equal(accounts, roundTripped);
        Assert.Equal(1, server.PutHits);
        Assert.Equal(1, server.GetHits);
    }

    [Fact]
    public async Task GetBlob_MissingName_ThrowsLoudly() {
        var svc = Make(new FakeServer());
        var ex = await Assert.ThrowsAsync<CloudSyncException>(
            () => svc.GetBlobAsync<KnownAccount[]>(Session(), "settings"));
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PutBlob_Unauthorized_ThrowsReconnect() {
        var server = new FakeServer { RejectAuth = true };
        var svc = Make(server);
        var ex = await Assert.ThrowsAsync<CloudSyncException>(
            () => svc.PutBlobAsync(Session(), "accounts", new[] { new KnownAccount("EI1", "A") }));
        Assert.Contains("reconnect", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetBlob_Unauthorized_ThrowsReconnect() {
        var server = new FakeServer { RejectAuth = true };
        var svc = Make(server);
        var ex = await Assert.ThrowsAsync<CloudSyncException>(
            () => svc.GetBlobAsync<KnownAccount[]>(Session(), "accounts"));
        Assert.Contains("reconnect", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListBlobs_AfterTwoPuts_ReturnsBothNames() {
        var server = new FakeServer();
        var svc = Make(server);
        var session = Session();
        await svc.PutBlobAsync(session, "accounts", new[] { 1 });
        await svc.PutBlobAsync(session, "settings", new[] { 2 });

        var list = await svc.ListBlobsAsync(session);
        Assert.Equal(new[] { "accounts", "settings" }, list.Select(e => e.Name).OrderBy(n => n));
    }

    [Fact]
    public async Task DeleteBlob_RemovesIt() {
        var server = new FakeServer();
        var svc = Make(server);
        var session = Session();
        await svc.PutBlobAsync(session, "reports", new[] { 1 });

        await svc.DeleteBlobAsync(session, "reports");
        Assert.False(server.Blobs.ContainsKey("reports"));
    }

    [Fact]
    public async Task DeleteAccount_ClearsAllBlobs() {
        var server = new FakeServer();
        var svc = Make(server);
        var session = Session();
        await svc.PutBlobAsync(session, "accounts", new[] { 1 });
        await svc.PutBlobAsync(session, "settings", new[] { 2 });

        await svc.DeleteAccountAsync(session);
        Assert.Empty(server.Blobs);
    }

    [Fact]
    public async Task BeginAuth_RedirectsBrowserAndReturnsState() {
        var server = new FakeServer();
        var nav = new FakeNavigation();
        var svc = Make(server, nav);

        var state = await svc.BeginAuthAsync();

        Assert.Equal("state-xyz", state);
        Assert.Equal("https://discord/oauth?state=state-xyz", nav.LastUrl);
    }

    [Fact]
    public async Task PollOnce_Pending_StaysPending() {
        var server = new FakeServer { PollPayload = null };
        var svc = Make(server);

        var result = await svc.PollOnceAsync("state-xyz");

        Assert.True(result.Pending);
        Assert.Null(result.Session);
    }

    [Fact]
    public async Task PollOnce_NotFound_TreatedAsPending() {
        // 404 means the pending row is not yet written; Go keeps polling.
        var server = new NotFoundPollServer();
        var svc = Make(server);

        var result = await svc.PollOnceAsync("state-xyz");

        Assert.True(result.Pending);
    }

    [Fact]
    public async Task PollOnce_Done_ReturnsSessionWithKey() {
        var server = new FakeServer {
            PollPayload = new PollResponse(Token, "user#1", "https://cdn/a.png", HexKey),
        };
        var svc = Make(server);

        var result = await svc.PollOnceAsync("state-xyz");

        Assert.False(result.Pending);
        Assert.NotNull(result.Session);
        Assert.Equal(Token, result.Session!.Token);
        Assert.Equal("user#1", result.Session.Username);
        Assert.Equal(HexKey, result.Session.EncryptionKey);
    }

    [Fact]
    public async Task FullFlow_PendingThenDone_ThenBlobUsesPolledKey() {
        // The state machine end to end: begin -> poll pending -> poll done ->
        // the returned session encrypts a blob that round-trips.
        var server = new FakeServer();
        var nav = new FakeNavigation();
        var svc = Make(server, nav);

        var state = await svc.BeginAuthAsync();
        Assert.NotNull(nav.LastUrl);

        var pending = await svc.PollOnceAsync(state);
        Assert.True(pending.Pending);

        server.PollPayload = new PollResponse(Token, "user#1", "https://cdn/a.png", HexKey);
        var done = await svc.PollOnceAsync(state);
        Assert.False(done.Pending);

        var session = done.Session!;
        await svc.PutBlobAsync(session, "accounts", new[] { new KnownAccount("EI1", "Alice") });
        var back = await svc.GetBlobAsync<KnownAccount[]>(session, "accounts");
        Assert.Equal("Alice", back[0].Name);
    }

    [Fact]
    public async Task BeginAuth_ServerError_ThrowsLoudly() {
        var svc = Make(new ErrorDiscordServer());
        await Assert.ThrowsAsync<CloudSyncException>(() => svc.BeginAuthAsync());
    }

    [Fact]
    public async Task Disconnect_SendsBearerAndDoesNotThrow() {
        var server = new BearerCapturingServer();
        var svc = Make(server);
        await svc.DisconnectAsync(Token);
        Assert.Equal($"Bearer {Token}", server.LastAuth);
    }

    [Fact]
    public async Task CheckReachable_Ok_ReturnsTrue() {
        var svc = Make(new VerifyServer(HttpStatusCode.OK));
        Assert.True(await svc.CheckReachableAsync());
    }

    [Fact]
    public async Task CheckReachable_NonOk_ReturnsFalse() {
        var svc = Make(new VerifyServer(HttpStatusCode.ServiceUnavailable));
        Assert.False(await svc.CheckReachableAsync());
    }

    [Fact]
    public async Task CheckReachable_TransportError_ReturnsFalse() {
        var svc = Make(new ThrowingServer());
        Assert.False(await svc.CheckReachableAsync());
    }

    private sealed class VerifyServer : HttpMessageHandler {
        private readonly HttpStatusCode _code;
        public VerifyServer(HttpStatusCode code) => _code = code;
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) {
            Assert.Equal("/api/v1/verify", request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(_code));
        }
    }

    private sealed class ThrowingServer : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("boom");
    }

    private sealed class NotFoundPollServer : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private sealed class ErrorDiscordServer : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
    }

    private sealed class BearerCapturingServer : HttpMessageHandler {
        public string? LastAuth;
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) {
            LastAuth = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }
    }
}
