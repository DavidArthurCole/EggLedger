using System.Text.Json;
using EggLedger.Domain.Api;
using EggLedger.Domain.Ei;
using Ei;
using Xunit;

namespace EggLedger.Domain.Tests.Api;

public class ApiPayloadJsonTests
{
    [Fact]
    public void FirstContact_JsonRoundTrips_ViaSharedOptions()
    {
        var fc = new EggIncFirstContactResponse
        {
            EiUserId = "EI1234567890123456",
            Backup = new Backup
            {
                artifacts = new Backup.Artifacts { LastFueledShip = MissionInfo.Spaceship.Henerprise },
                game = new Backup.Game { SoulEggsD = 1234.5, EggsOfProphecy = 7 },
            },
        };

        string json = JsonSerializer.Serialize(fc, ApiPayloadJson.Options);
        var back = JsonSerializer.Deserialize<EggIncFirstContactResponse>(json, ApiPayloadJson.Options)!;

        Assert.Equal("EI1234567890123456", back.EiUserId);
        Assert.Equal(MissionInfo.Spaceship.Henerprise, back.Backup!.artifacts!.LastFueledShip);
        Assert.Equal(1234.5, back.Backup.game!.SoulEggsD);
        Assert.Equal(7u, back.Backup.game.EggsOfProphecy);
    }

    [Fact]
    public void CompleteMission_JsonRoundTrips_ViaSharedOptions()
    {
        var resp = new CompleteMissionResponse
        {
            Success = true,
            EiUserId = "EI999",
            Info = new MissionInfo { Identifier = "m-1", Ship = MissionInfo.Spaceship.Henerprise },
        };

        string json = JsonSerializer.Serialize(resp, ApiPayloadJson.Options);
        var back = JsonSerializer.Deserialize<CompleteMissionResponse>(json, ApiPayloadJson.Options)!;

        Assert.True(back.Success);
        Assert.Equal("m-1", back.Info!.Identifier);
        Assert.Equal(MissionInfo.Spaceship.Henerprise, back.Info.Ship);
    }

    // Regression guard: get-only collection properties must survive the round-trip.
    // Without PreferredObjectCreationHandling.Populate, STJ leaves them empty.
    [Fact]
    public void GetOnlyCollections_SurviveRoundTrip()
    {
        var resp = new CompleteMissionResponse { Success = true };
        resp.Artifacts.Add(new CompleteMissionResponse.SecureArtifactSpec());
        resp.Artifacts.Add(new CompleteMissionResponse.SecureArtifactSpec());

        string json = JsonSerializer.Serialize(resp, ApiPayloadJson.Options);
        var back = JsonSerializer.Deserialize<CompleteMissionResponse>(json, ApiPayloadJson.Options)!;

        Assert.Equal(2, back.Artifacts.Count);
    }
}
