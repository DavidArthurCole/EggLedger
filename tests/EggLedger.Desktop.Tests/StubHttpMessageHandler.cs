using System.Net;

namespace EggLedger.Desktop.Tests;

public sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler {
    public List<string> RequestedUrls { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        RequestedUrls.Add(request.RequestUri?.ToString() ?? "");
        return Task.FromResult(responder(request));
    }

    public static HttpResponseMessage Json(string body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    public static HttpResponseMessage Bytes(byte[] data) {
        var content = new ByteArrayContent(data);
        content.Headers.ContentLength = data.Length;
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }
}
