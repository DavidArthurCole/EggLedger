using System.Net;
using EggLedger.Web.Server.Sync.Db;
using EggLedger.Web.Server.Tests.Sync.Auth;
using Npgsql;
using SyncKit.Identity.Client;

namespace EggLedger.Web.Server.Tests.Sync.Db;

/// <summary>
/// LookupAsync must resolve `user_id`, not `discord_id`, from the `sessions` table: SyncKit.Auth's
/// ISessionStore tuple still calls the field "DiscordId" (an external package, can't rename it),
/// but this migration repurposes that slot to carry the provider-neutral user id.
/// Runs only against a disposable Postgres at EGGLEDGER_TEST_DB_URL (unique schema created
/// and dropped, never prod); skipped when the env var is unset.
/// </summary>
public sealed class SessionStoreTests {
    private static string? TestDbUrl => Environment.GetEnvironmentVariable("EGGLEDGER_TEST_DB_URL");

    private const string Schema = "eltest_sessionstore";

    private static IdentityApiClient StubIdentity(bool revoked) =>
        new(new HttpClient(new StubHttpMessageHandler(_ =>
            StubHttpMessageHandler.Json(HttpStatusCode.OK, revoked ? "true" : "false"))) {
            BaseAddress = new Uri("http://localhost:8090"),
        });

    [SkippableFact]
    public async Task LookupAsync_returns_user_id_not_discord_id() {
        Skip.If(string.IsNullOrEmpty(TestDbUrl), "EGGLEDGER_TEST_DB_URL not set; live Postgres session test skipped.");

        await using var setupSrc = NpgsqlDataSource.Create(TestDbUrl!);
        await CreateSchemaAsync(setupSrc);

        var scopedBuilder = new NpgsqlConnectionStringBuilder(TestDbUrl!) { SearchPath = Schema };
        await using var src = NpgsqlDataSource.Create(scopedBuilder.ConnectionString);
        try {
            var userId = Guid.NewGuid();
            const string discordId = "99999999";
            const string token = "tok-abc";
            var expiresAt = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds();

            await Exec(src, $"""
                INSERT INTO users (user_id, discord_id, created_at) VALUES ('{userId}', '{discordId}', 0);
                INSERT INTO sessions (token, discord_id, user_id, expires_at)
                VALUES ('{token}', '{discordId}', '{userId}', {expiresAt});
                """);

            var store = new SessionStore(src, StubIdentity(revoked: false));
            var (found, returnedId, returnedExpiresAt) = await store.LookupAsync(token, CancellationToken.None);

            Assert.True(found);
            Assert.Equal(userId.ToString(), returnedId);
            Assert.NotEqual(discordId, returnedId);
            Assert.Equal(expiresAt, returnedExpiresAt);
        } finally {
            await DropSchemaAsync(setupSrc);
        }
    }

    [SkippableFact]
    public async Task LookupAsync_returns_not_found_when_identity_reports_session_revoked() {
        Skip.If(string.IsNullOrEmpty(TestDbUrl), "EGGLEDGER_TEST_DB_URL not set; live Postgres session test skipped.");

        await using var setupSrc = NpgsqlDataSource.Create(TestDbUrl!);
        await CreateSchemaAsync(setupSrc);

        var scopedBuilder = new NpgsqlConnectionStringBuilder(TestDbUrl!) { SearchPath = Schema };
        await using var src = NpgsqlDataSource.Create(scopedBuilder.ConnectionString);
        try {
            var userId = Guid.NewGuid();
            const string discordId = "99999999";
            const string token = "tok-revoked";
            var expiresAt = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds();

            await Exec(src, $"""
                INSERT INTO users (user_id, discord_id, created_at) VALUES ('{userId}', '{discordId}', 0);
                INSERT INTO sessions (token, discord_id, user_id, expires_at)
                VALUES ('{token}', '{discordId}', '{userId}', {expiresAt});
                """);

            var store = new SessionStore(src, StubIdentity(revoked: true));
            var (found, returnedId, returnedExpiresAt) = await store.LookupAsync(token, CancellationToken.None);

            Assert.False(found);
            Assert.Equal(string.Empty, returnedId);
            Assert.Equal(0, returnedExpiresAt);
        } finally {
            await DropSchemaAsync(setupSrc);
        }
    }

    private static async Task CreateSchemaAsync(NpgsqlDataSource src) {
        await Exec(src, $"DROP SCHEMA IF EXISTS {Schema} CASCADE; CREATE SCHEMA {Schema}; SET search_path TO {Schema};");
        await ApplyMigrationAsync(src, "1_initial_schema.up.sql");
        await ApplyMigrationAsync(src, "2_add_user_profile.up.sql");
        await ApplyMigrationAsync(src, "3_add_encryption_key.up.sql");
        await ApplyMigrationAsync(src, "4_eggledger_storage.up.sql");
        await ApplyMigrationAsync(src, "5_data_protection_keys.up.sql");
        await ApplyMigrationAsync(src, "6_api_spam_log.up.sql");
        await ApplyMigrationAsync(src, "8_identities.up.sql");
        await ApplyMigrationAsync(src, "9_identity_user_id_cascade.up.sql");
        await ApplyMigrationAsync(src, "10_identities_user_id_cascade.up.sql");
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
