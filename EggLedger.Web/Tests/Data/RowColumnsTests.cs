using EggLedger.Web.Data;

namespace EggLedger.Web.Tests.Data;

public sealed class RowColumnsTests {
    [Fact]
    public void Of_MissionRow_IncludesPayload() {
        Assert.Contains("complete_payload", RowColumns.Of<MissionRow>());
        Assert.Contains("player_id", RowColumns.Of<MissionRow>());
    }

    [Fact]
    public void Of_MissionMetaRow_OmitsPayload() {
        var cols = RowColumns.Of<MissionMetaRow>();
        Assert.DoesNotContain("complete_payload", cols);
        Assert.Contains("player_id", cols);
        Assert.Contains("ship", cols);
    }
}
