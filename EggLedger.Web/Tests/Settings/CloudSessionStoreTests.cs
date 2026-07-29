using EggLedger.Web.Data;
using EggLedger.Web.Services;
using EggLedger.Web.Settings;
using EggLedger.Web.Tests.Data;

namespace EggLedger.Web.Tests.Settings;

public sealed class CloudSessionStoreTests {
    private static CloudSessionStore Make() => new(new IndexedDbSettings(new FakeIndexedDb()));

    private static CloudSession Session() =>
        new("tok-1", "user#1", "https://cdn/a.png", "deadbeef");

    [Fact]
    public async Task GetSession_WhenNothingStored_ReturnsNull() {
        var store = Make();
        Assert.Null(await store.GetSessionAsync());
    }

    [Fact]
    public async Task SaveThenGetSession_RoundTrips() {
        var store = Make();
        await store.SaveSessionAsync(Session());

        var got = await store.GetSessionAsync();
        Assert.NotNull(got);
        Assert.Equal("tok-1", got!.Token);
        Assert.Equal("user#1", got.Username);
        Assert.Equal("https://cdn/a.png", got.AvatarUrl);
        Assert.Equal("deadbeef", got.EncryptionKey);
    }

    [Fact]
    public async Task UsesGoSettingKeys() {
        var settings = new IndexedDbSettings(new FakeIndexedDb());
        var store = new CloudSessionStore(settings);
        await store.SaveSessionAsync(Session());

        var all = await settings.GetAllSettingsAsync();
        Assert.Equal("tok-1", all[CloudSessionStore.KeyToken]);
        Assert.Equal("deadbeef", all[CloudSessionStore.KeyEncryptionKey]);
        Assert.Equal("user#1", all[CloudSessionStore.KeyUsername]);
        Assert.Equal("https://cdn/a.png", all[CloudSessionStore.KeyAvatarUrl]);
        Assert.Equal("cloud_session_token", CloudSessionStore.KeyToken);
        Assert.Equal("cloud_encryption_key", CloudSessionStore.KeyEncryptionKey);
    }

    [Fact]
    public async Task ClearSession_RemovesCreds() {
        var store = Make();
        await store.SaveSessionAsync(Session());
        await store.ClearSessionAsync();

        Assert.Null(await store.GetSessionAsync());
    }

    [Fact]
    public async Task Timestamps_PersistAndDefaultToZero() {
        var store = Make();
        Assert.Equal(0, await store.GetLastPushAtAsync());
        Assert.Equal(0, await store.GetLastPullAtAsync());

        await store.SetLastPushAtAsync(1_700_000_000);
        await store.SetLastPullAtAsync(1_700_000_500);

        Assert.Equal(1_700_000_000, await store.GetLastPushAtAsync());
        Assert.Equal(1_700_000_500, await store.GetLastPullAtAsync());
    }

    [Fact]
    public async Task PendingAuthState_PersistsAndClears() {
        var store = Make();
        Assert.Null(await store.GetPendingAuthStateAsync());

        await store.SetPendingAuthStateAsync("state-xyz");
        Assert.Equal("state-xyz", await store.GetPendingAuthStateAsync());

        await store.ClearPendingAuthStateAsync();
        Assert.Null(await store.GetPendingAuthStateAsync());
    }

    [Fact]
    public async Task AutoSync_PersistsAndDefaultsFalse() {
        var store = Make();
        Assert.False(await store.GetAutoSyncAsync());

        await store.SetAutoSyncAsync(true);
        Assert.True(await store.GetAutoSyncAsync());

        await store.SetAutoSyncAsync(false);
        Assert.False(await store.GetAutoSyncAsync());
    }
}
