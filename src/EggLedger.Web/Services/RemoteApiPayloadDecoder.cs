using System.Net.Http.Json;
using EggLedger.Domain.Api;
using EggLedger.Domain.Ei;
using Ei;

namespace EggLedger.Web.Services;

/// <summary>WASM decode path: protobuf-net cannot decode in the browser (Reflection.Emit is forbidden), so the raw payload is POSTed to the server's stateless decode endpoint and the JSON rehydrated via <see cref="ApiPayloadJson.Options"/>.</summary>
public sealed class RemoteApiPayloadDecoder : IApiPayloadDecoder {
    private readonly HttpClient _http;

    public RemoteApiPayloadDecoder(HttpClient http) => _http = http;

    public Task<EggIncFirstContactResponse> DecodeFirstContactAsync(byte[] rawPayload, CancellationToken ct = default) =>
        PostAsync<EggIncFirstContactResponse>("/api/v1/decode/first-contact", rawPayload, ct);

    public Task<CompleteMissionResponse> DecodeCompleteMissionAsync(byte[] rawPayload, CancellationToken ct = default) =>
        PostAsync<CompleteMissionResponse>("/api/v1/decode/complete-mission", rawPayload, ct);

    private async Task<T> PostAsync<T>(string path, byte[] rawPayload, CancellationToken ct) {
        using var content = new ByteArrayContent(rawPayload);
        using var resp = await _http.PostAsync(path, content, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content
            .ReadFromJsonAsync<T>(ApiPayloadJson.Options, ct)
            .ConfigureAwait(false);
        return result ?? throw new InvalidOperationException($"decode {path}: empty response");
    }
}
