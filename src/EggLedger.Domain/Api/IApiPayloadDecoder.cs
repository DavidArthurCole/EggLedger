using EggLedger.Domain.Ei;
using Ei;

namespace EggLedger.Domain.Api;

/// <summary>
/// Turns a raw (base64-decoded) Egg Inc API payload into the typed response.
/// The WASM host cannot run protobuf-net decode (Reflection.Emit is forbidden),
/// so it uses a remote implementation that delegates to the sync server; the
/// desktop host and tests use the local protobuf-net path.
/// </summary>
public interface IApiPayloadDecoder
{
    Task<EggIncFirstContactResponse> DecodeFirstContactAsync(byte[] rawPayload, CancellationToken ct = default);

    Task<CompleteMissionResponse> DecodeCompleteMissionAsync(byte[] rawPayload, CancellationToken ct = default);
}
