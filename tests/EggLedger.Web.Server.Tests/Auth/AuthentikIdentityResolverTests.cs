using EggLedger.Web.Server.Auth;
using Npgsql;

namespace EggLedger.Web.Server.Tests.Auth;

public sealed class AuthentikIdentityResolverTests {
    private static string? TestDbUrl => Environment.GetEnvironmentVariable("EGGLEDGER_TEST_DB_URL");
    private const string Schema = "eltest_identityresolver";

    [SkippableFact]
    public async Task ResolveAsync_returns_existing_user_id_on_exact_authentik_match() {
        Skip.If(string.IsNullOrEmpty(TestDbUrl), "EGGLEDGER_TEST_DB_URL not set; live Postgres test skipped.");
        await using var setupSrc = NpgsqlDataSource.Create(TestDbUrl!);
        await CreateSchemaAsync(setupSrc);
        var scopedBuilder = new NpgsqlConnectionStringBuilder(TestDbUrl!) { SearchPath = Schema };
        await using var src = NpgsqlDataSource.Create(scopedBuilder.ConnectionString);
        try {
            var existingUserId = Guid.NewGuid();
            await Exec(src, $"""
                INSERT INTO users (user_id, created_at) VALUES ('{existingUserId}', 0);
                INSERT INTO identities (user_id, provider, subject) VALUES ('{existingUserId}', 'authentik', 'sub-1');
                """);

            var resolver = new AuthentikIdentityResolver(src);
            var result = await resolver.ResolveAsync("sub-1", discordId: null, CancellationToken.None);

            Assert.Equal(existingUserId, result);
        } finally {
            await DropSchemaAsync(setupSrc);
        }
    }

    [SkippableFact]
    public async Task ResolveAsync_auto_links_when_discord_id_matches_existing_identity() {
        Skip.If(string.IsNullOrEmpty(TestDbUrl), "EGGLEDGER_TEST_DB_URL not set; live Postgres test skipped.");
        await using var setupSrc = NpgsqlDataSource.Create(TestDbUrl!);
        await CreateSchemaAsync(setupSrc);
        var scopedBuilder = new NpgsqlConnectionStringBuilder(TestDbUrl!) { SearchPath = Schema };
        await using var src = NpgsqlDataSource.Create(scopedBuilder.ConnectionString);
        try {
            var existingUserId = Guid.NewGuid();
            await Exec(src, $"""
                INSERT INTO users (user_id, discord_id, created_at) VALUES ('{existingUserId}', '999', 0);
                INSERT INTO identities (user_id, provider, subject) VALUES ('{existingUserId}', 'discord', '999');
                """);

            var resolver = new AuthentikIdentityResolver(src);
            var result = await resolver.ResolveAsync("sub-2", discordId: "999", CancellationToken.None);

            Assert.Equal(existingUserId, result);

            await using var cmd = src.CreateCommand("SELECT user_id FROM identities WHERE provider = 'authentik' AND subject = 'sub-2'");
            var linked = await cmd.ExecuteScalarAsync();
            Assert.Equal(existingUserId, Assert.IsType<Guid>(linked));
        } finally {
            await DropSchemaAsync(setupSrc);
        }
    }

    [SkippableFact]
    public async Task ResolveAsync_creates_new_user_when_no_match() {
        Skip.If(string.IsNullOrEmpty(TestDbUrl), "EGGLEDGER_TEST_DB_URL not set; live Postgres test skipped.");
        await using var setupSrc = NpgsqlDataSource.Create(TestDbUrl!);
        await CreateSchemaAsync(setupSrc);
        var scopedBuilder = new NpgsqlConnectionStringBuilder(TestDbUrl!) { SearchPath = Schema };
        await using var src = NpgsqlDataSource.Create(scopedBuilder.ConnectionString);
        try {
            var resolver = new AuthentikIdentityResolver(src);
            var result = await resolver.ResolveAsync("sub-3", discordId: null, CancellationToken.None);

            Assert.NotEqual(Guid.Empty, result);
            await using var cmd = src.CreateCommand("SELECT user_id FROM identities WHERE provider = 'authentik' AND subject = 'sub-3'");
            var linked = await cmd.ExecuteScalarAsync();
            Assert.Equal(result, Assert.IsType<Guid>(linked));
        } finally {
            await DropSchemaAsync(setupSrc);
        }
    }

    [SkippableFact]
    public async Task ResolveAsync_two_concurrent_first_logins_for_same_sub_agree_on_one_user_id() {
        Skip.If(string.IsNullOrEmpty(TestDbUrl), "EGGLEDGER_TEST_DB_URL not set; live Postgres test skipped.");
        await using var setupSrc = NpgsqlDataSource.Create(TestDbUrl!);
        await CreateSchemaAsync(setupSrc);
        var scopedBuilder = new NpgsqlConnectionStringBuilder(TestDbUrl!) { SearchPath = Schema };
        // Two independent data sources (like two independent requests) racing to resolve the
        // exact same brand-new 'sub'. Before the fix, both could return different user_ids with
        // only one reachable via `identities`; the fix's re-select-on-conflict guarantees the
        // loser hands back the winner's user_id instead of an orphaned one.
        await using var srcA = NpgsqlDataSource.Create(scopedBuilder.ConnectionString);
        await using var srcB = NpgsqlDataSource.Create(scopedBuilder.ConnectionString);
        try {
            var resolverA = new AuthentikIdentityResolver(srcA);
            var resolverB = new AuthentikIdentityResolver(srcB);

            var taskA = resolverA.ResolveAsync("sub-race", discordId: null, CancellationToken.None);
            var taskB = resolverB.ResolveAsync("sub-race", discordId: null, CancellationToken.None);
            var results = await Task.WhenAll(taskA, taskB);

            Assert.Equal(results[0], results[1]);
            await using var cmd = srcA.CreateCommand("SELECT COUNT(*) FROM identities WHERE provider = 'authentik' AND subject = 'sub-race'");
            var count = (long)(await cmd.ExecuteScalarAsync())!;
            Assert.Equal(1, count);
        } finally {
            await DropSchemaAsync(setupSrc);
        }
    }

    private static async Task CreateSchemaAsync(NpgsqlDataSource src) {
        await Exec(src, $"DROP SCHEMA IF EXISTS {Schema} CASCADE; CREATE SCHEMA {Schema}; SET search_path TO {Schema};");
        foreach (var file in new[] {
            "1_initial_schema.up.sql", "2_add_user_profile.up.sql", "3_add_encryption_key.up.sql",
            "4_eggledger_storage.up.sql", "5_data_protection_keys.up.sql", "6_api_spam_log.up.sql",
            "7_identities.up.sql",
        }) {
            var sqlPath = Path.Combine(AppContext.BaseDirectory, "Migrations", file);
            var sql = await File.ReadAllTextAsync(sqlPath);
            await Exec(src, $"SET search_path TO {Schema}; {sql}");
        }
    }

    private static async Task DropSchemaAsync(NpgsqlDataSource src) =>
        await Exec(src, $"DROP SCHEMA IF EXISTS {Schema} CASCADE;");

    private static async Task Exec(NpgsqlDataSource src, string sql) {
        await using var cmd = src.CreateCommand(sql);
        await cmd.ExecuteNonQueryAsync();
    }
}
