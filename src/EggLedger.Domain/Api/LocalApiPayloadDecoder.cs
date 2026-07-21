using EggLedger.Domain.Ei;
using Ei;

namespace EggLedger.Domain.Api;

public sealed class LocalApiPayloadDecoder : IApiPayloadDecoder {
    private readonly ApiClient _api;

    public LocalApiPayloadDecoder(ApiClient api) => _api = api;

    public Task<EggIncFirstContactResponse> DecodeFirstContactAsync(byte[] rawPayload, CancellationToken ct = default) =>
        Task.FromResult(_api.DecodeFirstContactPayload(rawPayload));

    public Task<CompleteMissionResponse> DecodeCompleteMissionAsync(byte[] rawPayload, CancellationToken ct = default) =>
        Task.FromResult(_api.DecodeCompleteMissionPayload(rawPayload));
}
