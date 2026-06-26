using Ei;

namespace EggLedger.Domain.Api;

/// <summary>
/// Port of Go api/api.go. The two endpoints used: unauthenticated first-contact and
/// authenticated complete-mission.
/// </summary>
public sealed partial class ApiClient {
    /// <summary>Unauthenticated; returns the player backup.</summary>
    public const string FirstContactEndpoint = "/ei/bot_first_contact";

    /// <summary>Authenticated; returns a completed mission.</summary>
    public const string CompleteMissionEndpoint = "/ei_afx/complete_mission";

    /// <summary>
    /// Requests the raw (base64-decoded) first-contact payload for a player.
    /// DeviceId is "EggLedger" (it is the bot_name for bot_first_contact).
    /// </summary>
    public Task<byte[]> RequestFirstContactRawPayloadAsync(
        string playerId,
        CancellationToken cancellationToken = default) {
        var req = new EggIncFirstContactRequest {
            Rinfo = NewBasicRequestInfo(playerId),
            EiUserId = playerId,
            DeviceId = "EggLedger",
            ClientVersion = ClientVersion,
            Platform = Platform,
        };
        return RequestRawPayloadAsync(FirstContactEndpoint, req, cancellationToken);
    }

    /// <summary>Decodes a first-contact payload (unauthenticated).</summary>
    public EggIncFirstContactResponse DecodeFirstContactPayload(byte[] payload) =>
        DecodeApiResponse<EggIncFirstContactResponse>(
            ApiPrefix + FirstContactEndpoint, payload, authenticated: false);

    /// <summary>
    /// Requests the raw (base64-decoded) complete-mission payload for a
    /// player+mission. The mission id is set on Info.Identifier.
    /// </summary>
    public Task<byte[]> RequestCompleteMissionRawPayloadAsync(
        string playerId,
        string missionId,
        CancellationToken cancellationToken = default) {
        var req = new MissionRequest {
            Rinfo = NewBasicRequestInfo(playerId),
            EiUserId = playerId,
            Info = new MissionInfo { Identifier = missionId },
            ClientVersion = ClientVersion,
        };
        return RequestRawPayloadAsync(CompleteMissionEndpoint, req, cancellationToken);
    }

    /// <summary>Decodes a complete-mission payload (authenticated, possibly zlib).</summary>
    public CompleteMissionResponse DecodeCompleteMissionPayload(byte[] payload) =>
        DecodeApiResponse<CompleteMissionResponse>(
            ApiPrefix + CompleteMissionEndpoint, payload, authenticated: true);
}
