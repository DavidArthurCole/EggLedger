using System.Net;
using System.Text;
using System.Text.Json;
using EggLedger.Domain.Api;
using EggLedger.Domain.Ei;
using Ei;
using EggLedger.Web.Services;
using Xunit;

namespace EggLedger.Web.Tests.Services;

public class RemoteApiPayloadDecoderTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _json;
        public string? LastPath;
        public byte[]? LastBody;
        public StubHandler(string json) => _json = json;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastPath = request.RequestUri!.AbsolutePath;
            LastBody = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json"),
            };
        }
    }

    [Fact]
    public async Task DecodeFirstContact_PostsBytes_ParsesJson()
    {
        var expected = new EggIncFirstContactResponse { EiUserId = "EI42" };
        string json = JsonSerializer.Serialize(expected, ApiPayloadJson.Options);
        var handler = new StubHandler(json);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://example/") };
        IApiPayloadDecoder decoder = new RemoteApiPayloadDecoder(http);

        var got = await decoder.DecodeFirstContactAsync(new byte[] { 1, 2, 3 });

        Assert.Equal("EI42", got.EiUserId);
        Assert.Equal("/api/v1/decode/first-contact", handler.LastPath);
        Assert.Equal(new byte[] { 1, 2, 3 }, handler.LastBody);
    }

    [Fact]
    public async Task DecodeCompleteMission_PostsBytes_ParsesJson()
    {
        var expected = new CompleteMissionResponse { Success = true, EiUserId = "EI99" };
        string json = JsonSerializer.Serialize(expected, ApiPayloadJson.Options);
        var handler = new StubHandler(json);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://example/") };
        IApiPayloadDecoder decoder = new RemoteApiPayloadDecoder(http);

        var got = await decoder.DecodeCompleteMissionAsync(new byte[] { 9 });

        Assert.True(got.Success);
        Assert.Equal("EI99", got.EiUserId);
        Assert.Equal("/api/v1/decode/complete-mission", handler.LastPath);
    }
}
