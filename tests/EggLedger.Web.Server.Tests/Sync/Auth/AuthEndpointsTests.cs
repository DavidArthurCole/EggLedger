using EggLedger.Web.Server.Sync.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using SyncKit.Auth;

namespace EggLedger.Web.Server.Tests.Sync.Auth;

/// <summary>
/// StorePending must create both the provider-neutral `users` row and a linked
/// `identities` row (provider='discord'), since login is no longer solely Discord-keyed.
/// Runs only against a disposable Postgres at EGGLEDGER_TEST_DB_URL (unique schema created
/// and dropped, never prod); skipped when the env var is unset.
/// </summary>
public sealed class AuthEndpointsTests {
    private static string? TestDbUrl => Environment.GetEnvironmentVariable("EGGLEDGER_TEST_DB_URL");

    private const string Schema = "eltest_authendpoints";

    [SkippableFact]
    public async Task StorePending_creates_user_and_discord_identity_row() {
        Skip.If(string.IsNullOrEmpty(TestDbUrl), "EGGLEDGER_TEST_DB_URL not set; live Postgres auth test skipped.");

        await using var setupSrc = NpgsqlDataSource.Create(TestDbUrl!);
        await CreateSchemaAsync(setupSrc);

        // AuthEndpoints issues plain commands with no SET search_path, so point this data
        // source's connections at the throwaway schema directly via the connection string.
        var scopedBuilder = new NpgsqlConnectionStringBuilder(TestDbUrl!) { SearchPath = Schema };
        await using var src = NpgsqlDataSource.Create(scopedBuilder.ConnectionString);
        try {
            var endpoints = new AuthEndpoints(src, new EphemeralDataProtectionProvider(), NullLogger<AuthEndpoints>.Instance);
            var discordUser = new DiscordUser("12345", "tester", "");

            await endpoints.StorePending("state-1", "token-1", discordUser);

            Guid userId;
            await using (var cmd = src.CreateCommand($"SET search_path TO {Schema}; SELECT user_id FROM users WHERE discord_id = '12345';")) {
                var result = await cmd.ExecuteScalarAsync();
                Assert.NotNull(result);
                userId = Assert.IsType<Guid>(result);
                Assert.NotEqual(Guid.Empty, userId);
            }

            await using (var cmd = src.CreateCommand($"SET search_path TO {Schema}; SELECT user_id FROM identities WHERE provider = 'discord' AND subject = '12345';")) {
                var result = await cmd.ExecuteScalarAsync();
                Assert.NotNull(result);
                Assert.Equal(userId, Assert.IsType<Guid>(result));
            }
        } finally {
            await DropSchemaAsync(setupSrc);
        }
    }

    // StorePending touches users.username/avatar_url/encryption_key (migrations 2, 3) and
    // sessions (migration 1) alongside user_id/identities (migration 7), so the full linear
    // chain must apply here, unlike PostgresIsolationTests/IdentitiesMigrationTests which only
    // need a subset of tables.
    private static async Task CreateSchemaAsync(NpgsqlDataSource src) {
        await Exec(src, $"DROP SCHEMA IF EXISTS {Schema} CASCADE; CREATE SCHEMA {Schema}; SET search_path TO {Schema};");
        await ApplyMigrationAsync(src, "1_initial_schema.up.sql");
        await ApplyMigrationAsync(src, "2_add_user_profile.up.sql");
        await ApplyMigrationAsync(src, "3_add_encryption_key.up.sql");
        await ApplyMigrationAsync(src, "4_eggledger_storage.up.sql");
        await ApplyMigrationAsync(src, "5_data_protection_keys.up.sql");
        await ApplyMigrationAsync(src, "6_api_spam_log.up.sql");
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
