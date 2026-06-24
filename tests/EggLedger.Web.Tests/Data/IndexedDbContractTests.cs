using EggLedger.Web.Data;

namespace EggLedger.Web.Tests.Data;

public sealed class IndexedDbContractTests {
    private static (IndexedDb Db, FakeJsObjectReference Module) Make() {
        var module = new FakeJsObjectReference();
        var runtime = new FakeJsRuntime(module);
        return (new IndexedDb(runtime), module);
    }

    private static (IndexedDb Db, FakeJsObjectReference Module, FakeJsRuntime Runtime) MakeWithRuntime() {
        var module = new FakeJsObjectReference();
        var runtime = new FakeJsRuntime(module);
        return (new IndexedDb(runtime), module, runtime);
    }

    [Fact]
    public async Task PutAsync_forwards_put_with_store_and_value() {
        var (db, module) = Make();
        var row = new { player_id = "p1", mission_id = "m1" };

        await db.PutAsync("mission", row);

        var call = Assert.Single(module.Calls);
        Assert.Equal("put", call.Identifier);
        Assert.Equal("mission", call.Args[0]);
        Assert.Same(row, call.Args[1]);
    }

    [Fact]
    public async Task PutManyAsync_forwards_putMany_and_returns_count() {
        var (db, module) = Make();
        var values = new object[] { new { id = 1 }, new { id = 2 } };
        module.Enqueue("putMany", 2);

        var count = await db.PutManyAsync("mission", values);

        Assert.Equal(2, count);
        var call = Assert.Single(module.Calls);
        Assert.Equal("putMany", call.Identifier);
        Assert.Equal("mission", call.Args[0]);
        Assert.Same(values, call.Args[1]);
    }

    [Fact]
    public async Task GetAsync_forwards_get_with_key_and_returns_value() {
        var (db, module) = Make();
        module.Enqueue("get", "hit");

        var result = await db.GetAsync<string>("settings", "theme");

        Assert.Equal("hit", result);
        var call = Assert.Single(module.Calls);
        Assert.Equal("get", call.Identifier);
        Assert.Equal("settings", call.Args[0]);
        Assert.Equal("theme", call.Args[1]);
    }

    [Fact]
    public async Task GetAllAsync_forwards_getAll() {
        var (db, module) = Make();
        module.Enqueue("getAll", new[] { "a", "b" });

        var result = await db.GetAllAsync<string>("backup");

        Assert.Equal(new[] { "a", "b" }, result);
        var call = Assert.Single(module.Calls);
        Assert.Equal("getAll", call.Identifier);
        Assert.Equal("backup", call.Args[0]);
    }

    [Fact]
    public async Task GetAllByIndexAsync_forwards_index_name_and_value() {
        var (db, module) = Make();
        module.Enqueue("getAllByIndex", new[] { "x" });

        var result = await db.GetAllByIndexAsync<string>("mission", "player_ship", "p1");

        Assert.Equal(new[] { "x" }, result);
        var call = Assert.Single(module.Calls);
        Assert.Equal("getAllByIndex", call.Identifier);
        Assert.Equal("mission", call.Args[0]);
        Assert.Equal("player_ship", call.Args[1]);
        Assert.Equal("p1", call.Args[2]);
    }

    [Fact]
    public async Task DeleteAsync_forwards_del() {
        var (db, module) = Make();

        await db.DeleteAsync("settings", "theme");

        var call = Assert.Single(module.Calls);
        Assert.Equal("del", call.Identifier);
        Assert.Equal("settings", call.Args[0]);
        Assert.Equal("theme", call.Args[1]);
    }

    [Fact]
    public async Task ClearAsync_forwards_clear() {
        var (db, module) = Make();

        await db.ClearAsync("mission");

        var call = Assert.Single(module.Calls);
        Assert.Equal("clear", call.Identifier);
        Assert.Equal("mission", call.Args[0]);
    }

    [Fact]
    public async Task CountAsync_forwards_count_and_returns_int() {
        var (db, module) = Make();
        module.Enqueue("count", 7);

        var count = await db.CountAsync("mission");

        Assert.Equal(7, count);
        var call = Assert.Single(module.Calls);
        Assert.Equal("count", call.Identifier);
        Assert.Equal("mission", call.Args[0]);
    }

    [Fact]
    public async Task Module_imported_once_across_calls() {
        var (db, _, runtime) = MakeWithRuntime();

        await db.CountAsync("mission");
        await db.CountAsync("backup");

        Assert.Single(runtime.Calls, c => c.Identifier == "import");
    }

    [Fact]
    public async Task DisposeAsync_disposes_module_when_loaded() {
        var (db, module) = Make();
        await db.CountAsync("mission");

        await db.DisposeAsync();

        Assert.True(module.Disposed);
    }
}
