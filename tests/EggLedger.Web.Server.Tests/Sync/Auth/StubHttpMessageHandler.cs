using System.Net;

namespace EggLedger.Web.Server.Tests.Sync.Auth;

// No HTTP-mocking helper existed anywhere in this test project before IdentityApiClient's
// tests needed one (confirmed: the one in EggLedger.Desktop.Tests is a separate project, for
// unrelated GitHub-release-check code). Routes by request path substring to a canned response.
public sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(respond(request));

    public static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
}
