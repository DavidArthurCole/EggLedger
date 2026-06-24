using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace EggLedger.Web.Server.Sync.Menno;

public sealed record MennoRequest(
    [property: JsonPropertyName("eid")] string Eid);

public sealed class MennoEndpoint(HttpClient client, string functionKey, string upstreamUrl)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task Submit(HttpContext ctx)
    {
        MennoRequest? body;
        try { body = await JsonSerializer.DeserializeAsync<MennoRequest>(ctx.Request.Body, Json, ctx.RequestAborted); }
        catch (JsonException)
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsync("invalid request body: eid is required\n", ctx.RequestAborted);
            return;
        }
        if (body is null || string.IsNullOrEmpty(body.Eid))
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsync("invalid request body: eid is required\n", ctx.RequestAborted);
            return;
        }
        using var req = new HttpRequestMessage(HttpMethod.Post, upstreamUrl)
        {
            Content = JsonContent.Create(new { eid = body.Eid }),
        };
        req.Headers.Add("x-functions-key", functionKey);
        try
        {
            using var resp = await client.SendAsync(req, ctx.RequestAborted);
            ctx.Response.StatusCode = (int)resp.StatusCode;
        }
        catch (HttpRequestException)
        {
            ctx.Response.StatusCode = StatusCodes.Status502BadGateway;
        }
    }
}
