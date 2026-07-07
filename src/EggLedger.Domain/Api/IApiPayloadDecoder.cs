using EggLedger.Domain.Ei;
using Ei;

namespace EggLedger.Domain.Api;

/// <summary>Turns a raw (base64-decoded) API payload into the typed response via the local protobuf-net path.</summary>
public interface IApiPayloadDecoder {
    Task<EggIncFirstContactResponse> DecodeFirstContactAsync(byte[] rawPayload, CancellationToken ct = default);
    Task<CompleteMissionResponse> DecodeCompleteMissionAsync(byte[] rawPayload, CancellationToken ct = default);
}
