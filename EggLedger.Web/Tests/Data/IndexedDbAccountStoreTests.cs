using EggLedger.Domain.MissionQuery;
using EggLedger.Web.Data;

namespace EggLedger.Web.Tests.Data;

public sealed class IndexedDbAccountStoreTests {
    private static IndexedDbAccountStore Make(FakeIndexedDb db) =>
        new(new IndexedDbSettings(db));

    private static AccountInfo Acct(string id, string nick = "Nick") =>
        new() { Id = id, Nickname = nick, EBString = "1.0q", AccountColor = "abc", SeString = "5.00B", PeCount = 3, TeCount = 1 };

    [Fact]
    public async Task EmptyByDefault() {
        var store = Make(new FakeIndexedDb());
        Assert.Empty(await store.GetKnownAccountsAsync());
    }

    [Fact]
    public async Task AddPersistsAndRoundTrips() {
        var store = Make(new FakeIndexedDb());
        await store.AddKnownAccountAsync(Acct("EI1", "Alice"));

        var got = await store.GetKnownAccountsAsync();

        Assert.Single(got);
        Assert.Equal("EI1", got[0].Id);
        Assert.Equal("Alice", got[0].Nickname);
        Assert.Equal(3, got[0].PeCount);
        Assert.Equal(1, got[0].TeCount);
    }

    [Fact]
    public async Task AddUpsertsByIdNotDuplicate() {
        var store = Make(new FakeIndexedDb());
        await store.AddKnownAccountAsync(Acct("EI1", "Old"));
        await store.AddKnownAccountAsync(Acct("EI1", "New"));

        var got = await store.GetKnownAccountsAsync();

        Assert.Single(got);
        Assert.Equal("New", got[0].Nickname);
    }

    [Fact]
    public async Task AddPreservesInsertionOrder() {
        var store = Make(new FakeIndexedDb());
        await store.AddKnownAccountAsync(Acct("EI1"));
        await store.AddKnownAccountAsync(Acct("EI2"));
        await store.AddKnownAccountAsync(Acct("EI3"));

        var got = await store.GetKnownAccountsAsync();

        Assert.Equal(new[] { "EI1", "EI2", "EI3" }, got.Select(a => a.Id));
    }

    [Fact]
    public async Task RemoveDropsAccount() {
        var store = Make(new FakeIndexedDb());
        await store.AddKnownAccountAsync(Acct("EI1"));
        await store.AddKnownAccountAsync(Acct("EI2"));

        await store.RemoveKnownAccountAsync("EI1");

        var got = await store.GetKnownAccountsAsync();
        Assert.Single(got);
        Assert.Equal("EI2", got[0].Id);
    }

    [Fact]
    public async Task CorruptBlobReturnsEmpty() {
        var db = new FakeIndexedDb();
        var settings = new IndexedDbSettings(db);
        await settings.SetSettingAsync("known_accounts", "not json{");

        var store = new IndexedDbAccountStore(settings);

        Assert.Empty(await store.GetKnownAccountsAsync());
    }

    [Fact]
    public async Task ActiveAccountRoundTrips() {
        var store = Make(new FakeIndexedDb());
        Assert.Null(await store.GetActiveAccountIdAsync());

        await store.SetActiveAccountIdAsync("EI9");
        Assert.Equal("EI9", await store.GetActiveAccountIdAsync());

        await store.SetActiveAccountIdAsync("");
        Assert.Null(await store.GetActiveAccountIdAsync());
    }
}
