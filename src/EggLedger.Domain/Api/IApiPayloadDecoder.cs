using EggLedger.Domain.Ei;
using Ei;

namespace EggLedger.Domain.Api;

/// <summary>
/// Turns a raw (base64-decoded) API payload into the typed response. WASM cannot run protobuf-net
/// decode (Reflection.Emit forbidden), so it delegates to the sync server; desktop and tests use the
/// local protobuf-net path.
/// </summary>
public interface IApiPayloadDecoder {
    Task<EggIncFirstContactResponse> DecodeFirstContactAsync(byte[] rawPayload, CancellationToken ct = default);

    Task<CompleteMissionResponse> DecodeCompleteMissionAsync(byte[] rawPayload, CancellationToken ct = default);
}
