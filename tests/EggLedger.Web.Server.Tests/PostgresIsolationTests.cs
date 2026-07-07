using System.Security.Claims;
using EggLedger.Web.Data;
using EggLedger.Web.Server.Auth;
using EggLedger.Web.Server.Storage;
using Microsoft.AspNetCore.Components.Authorization;
using Npgsql;

namespace EggLedger.Web.Server.Tests;

/// <summary>
/// Security-critical cross-user isolation for the multi-tenant PostgresIndexedDb: user A must never read user B's rows.
/// Runs only against a disposable Postgres at EGGLEDGER_TEST_DB_URL (unique schema created and dropped, never prod); skipped when the env var is unset.
/// </summary>
public sealed class PostgresIsolationTests {
    private static string? TestDbUrl => Environment.GetEnvironmentVariable("EGGLEDGER_TEST_DB_URL");

    // Fixed auth principal for one user id, mimicking the cookie-auth claim.
    private sealed class FakeAuth(Guid? userId) : AuthenticationStateProvider {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() {
            var identity = userId is null
                ? new ClaimsIdentity()
                : new ClaimsIdentity([new Claim(AuthScheme.UserIdClaim, userId.Value.ToString())], "test");
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
        }
    }

    private static PostgresIndexedDb StoreFor(NpgsqlDataSource src, Guid? userId) =>
        new(src, new CurrentUser(new FakeAuth(userId)));

    private static MissionRow Mission(string player, string id) => new() {
        PlayerId = player,
        MissionId = id,
        StartTimestamp = 1,
        CompletePayload = [1, 2, 3],
        MissionType = 0,
    };

    [SkippableFact]
    public async Task UserA_NeverReadsUserB_Rows() {
        Skip.If(string.IsNullOrEmpty(TestDbUrl), "EGGLEDGER_TEST_DB_URL not set; live Postgres isolation test skipped.");

        await using var src = NpgsqlDataSource.Create(TestDbUrl!);
        await CreateSchemaAsync(src);
        try {
            var a = StoreFor(src, Guid.NewGuid());
            var b = StoreFor(src, Guid.NewGuid());

            await a.PutAsync("mission", Mission("EI_A", "m1"));
            await a.PutAsync("mission", Mission("EI_A", "m2"));
            await b.PutAsync("mission", Mission("EI_B", "m9"));

            var aAll = await a.GetAllAsync<MissionRow>("mission");
            var bAll = await b.GetAllAsync<MissionRow>("mission");

            Assert.Equal(2, aAll.Length);
            Assert.All(aAll, m => Assert.Equal("EI_A", m.PlayerId));
            Assert.Single(bAll);
            Assert.Equal("EI_B", bAll[0].PlayerId);

            // B must not see A's mission even by exact key.
            var leaked = await b.GetAsync<MissionRow>("mission", new object[] { "EI_A", "m1" });
            Assert.Null(leaked);

            // A's count is scoped.
            Assert.Equal(2, await a.CountAsync("mission"));
            Assert.Equal(1, await b.CountAsync("mission"));

            // B clearing its store leaves A's rows intact.
            await b.ClearAsync("mission");
            Assert.Equal(2, (await a.GetAllAsync<MissionRow>("mission")).Length);
            Assert.Empty(await b.GetAllAsync<MissionRow>("mission"));
        } finally {
            await DropSchemaAsync(src);
        }
    }

    [SkippableFact]
    public async Task Unauthenticated_StorageThrows() {
        Skip.If(string.IsNullOrEmpty(TestDbUrl), "EGGLEDGER_TEST_DB_URL not set; live Postgres isolation test skipped.");

        await using var src = NpgsqlDataSource.Create(TestDbUrl!);
        await CreateSchemaAsync(src);
        try {
            var anon = StoreFor(src, null);
            // Reads tolerate the gated state (return empty); writes must throw.
            Assert.Empty(await anon.GetAllAsync<MissionRow>("mission"));
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await anon.PutAsync("mission", Mission("EI_X", "x1")));
        } finally {
            await DropSchemaAsync(src);
        }
    }

    // Runs the storage migration into a throwaway schema named per test run so concurrent
    // runs and prod tables never collide. search_path scopes the el_* tables to it.
    private const string Schema = "eltest_iso";

    private static async Task CreateSchemaAsync(NpgsqlDataSource src) {
        await Exec(src, $"DROP SCHEMA IF EXISTS {Schema} CASCADE; CREATE SCHEMA {Schema}; SET search_path TO {Schema};");
        // user_id only exists after migration 7 backfills it onto the migration-4 tables.
        await ApplyMigrationAsync(src, "1_initial_schema.up.sql");
        await ApplyMigrationAsync(src, "4_eggledger_storage.up.sql");
        await ApplyMigrationAsync(src, "7_identities.up.sql");
    }

    private static async Task ApplyMigrationAsync(NpgsqlDataSource src, string fileName) {
        var sqlPath = Path.Combine(AppContext.BaseDirectory, "Migrations", fileName);
        var sql = await File.ReadAllTextAsync(sqlPath);
        await Exec(src, $"SET search_path TO {Schema}; {sql}");
    }

    private static async Task DropSchemaAsync(NpgsqlDataSource src) =>
        await Exec(src, $"DROP SCHEMA IF EXISTS {Schema} CASCADE;");

    private static async Task Exec(NpgsqlDataSource src, string sql) {
        await using var cmd = src.CreateCommand(sql);
        await cmd.ExecuteNonQueryAsync();
    }
}
