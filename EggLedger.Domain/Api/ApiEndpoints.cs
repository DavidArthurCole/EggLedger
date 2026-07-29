using Ei;

namespace EggLedger.Domain.Api;

public sealed partial class ApiClient {
    public const string FirstContactEndpoint = "/ei/bot_first_contact";

    public const string CompleteMissionEndpoint = "/ei_afx/complete_mission";

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

    public EggIncFirstContactResponse DecodeFirstContactPayload(byte[] payload) =>
        DecodeApiResponse<EggIncFirstContactResponse>(
            ApiPrefix + FirstContactEndpoint, payload, authenticated: false);

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

    public CompleteMissionResponse DecodeCompleteMissionPayload(byte[] payload) =>
        DecodeApiResponse<CompleteMissionResponse>(
            ApiPrefix + CompleteMissionEndpoint, payload, authenticated: true);
}
