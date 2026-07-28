using EggLedger.Web.Data;

namespace EggLedger.Web.Tests.Data;

public sealed class IndexedDbSettingsTests {
    [Fact]
    public async Task SetSetting_ThenGetAll_ReturnsThePair() {
        var settings = new IndexedDbSettings(new FakeIndexedDb());

        await settings.SetSettingAsync("theme", "dark");

        var all = await settings.GetAllSettingsAsync();
        Assert.Single(all);
        Assert.Equal("dark", all["theme"]);
    }

    [Fact]
    public async Task GetAll_Empty_ReturnsEmptyDictionary() {
        var settings = new IndexedDbSettings(new FakeIndexedDb());

        var all = await settings.GetAllSettingsAsync();

        Assert.Empty(all);
    }

    [Fact]
    public async Task SetSettings_Batch_ThenGetAll_ReturnsAll() {
        var settings = new IndexedDbSettings(new FakeIndexedDb());

        await settings.SetSettingsAsync(new Dictionary<string, string> {
            ["theme"] = "dark",
            ["eidMasked"] = "true",
            ["lastEid"] = "EI1234",
        });

        var all = await settings.GetAllSettingsAsync();
        Assert.Equal(3, all.Count);
        Assert.Equal("dark", all["theme"]);
        Assert.Equal("true", all["eidMasked"]);
        Assert.Equal("EI1234", all["lastEid"]);
    }

    [Fact]
    public async Task SetSetting_Overwrite_UpdatesNotDuplicates() {
        var db = new FakeIndexedDb();
        var settings = new IndexedDbSettings(db);

        await settings.SetSettingAsync("theme", "dark");
        await settings.SetSettingAsync("theme", "light");

        var all = await settings.GetAllSettingsAsync();
        Assert.Single(all);
        Assert.Equal("light", all["theme"]);
    }

    [Fact]
    public async Task SetSettings_Batch_Overwrite_UpdatesNotDuplicates() {
        var settings = new IndexedDbSettings(new FakeIndexedDb());

        await settings.SetSettingAsync("theme", "dark");
        await settings.SetSettingsAsync(new Dictionary<string, string> {
            ["theme"] = "light",
            ["lastEid"] = "EI1234",
        });

        var all = await settings.GetAllSettingsAsync();
        Assert.Equal(2, all.Count);
        Assert.Equal("light", all["theme"]);
        Assert.Equal("EI1234", all["lastEid"]);
    }
}
