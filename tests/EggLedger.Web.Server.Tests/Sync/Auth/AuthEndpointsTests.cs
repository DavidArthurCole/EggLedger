using System.Net;
using System.Security.Claims;
using System.Text.Json;
using EggLedger.Web.Server.Sync.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using SyncKit.Auth;
using SyncKit.Identity.Client;

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

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
            // Identity resolution (user_id minting + the identities link) now happens via
            // SyncKit.Identity, not local INSERTs - stub its response so this test stays a pure
            // StorePending unit test (users/encryption_key/sessions writes) without a live API.
            var stubUserId = Guid.NewGuid();
            var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(HttpStatusCode.OK,
                $$"""{"userId":"{{stubUserId}}","role":"viewer","discordId":"12345","isNew":true}"""));
            var identity = new IdentityApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8090") });

            var endpoints = new AuthEndpoints(src, new EphemeralDataProtectionProvider(), identity, NullLogger<AuthEndpoints>.Instance);
            var discordUser = new DiscordUser("12345", "tester", "");

            var (userId, role) = await endpoints.StorePending("state-1", "token-1", discordUser);

            Assert.Equal(stubUserId, userId);
            Assert.Equal("viewer", role);

            await using var cmd = src.CreateCommand($"SET search_path TO {Schema}; SELECT user_id FROM users WHERE discord_id = '12345';");
            var result = await cmd.ExecuteScalarAsync();
            Assert.Equal(stubUserId, Assert.IsType<Guid>(result));
        } finally {
            await DropSchemaAsync(setupSrc);
        }
    }

    // Regression guard for the user_id-keyed lookup fix: an existing Discord-linked user
    // (row already has both discord_id and user_id) must get back the SAME key it already had,
    // not a freshly generated one, since the lookup now goes through user_id instead of discord_id.
    [SkippableFact]
    public async Task EnsureEncryptionKeyAsync_returns_existing_key_for_discord_linked_user() {
        Skip.If(string.IsNullOrEmpty(TestDbUrl), "EGGLEDGER_TEST_DB_URL not set; live Postgres auth test skipped.");

        await using var setupSrc = NpgsqlDataSource.Create(TestDbUrl!);
        await CreateSchemaAsync(setupSrc);

        var scopedBuilder = new NpgsqlConnectionStringBuilder(TestDbUrl!) { SearchPath = Schema };
        await using var src = NpgsqlDataSource.Create(scopedBuilder.ConnectionString);
        try {
            var protector = new EphemeralDataProtectionProvider();
            var identity = new IdentityApiClient(new HttpClient(new StubHttpMessageHandler(_ =>
                StubHttpMessageHandler.Json(HttpStatusCode.OK, "{}"))) { BaseAddress = new Uri("http://localhost:8090") });
            var endpoints = new AuthEndpoints(src, protector, identity, NullLogger<AuthEndpoints>.Instance);

            var userId = Guid.NewGuid();
            var protectedKey = protector.CreateProtector("EggLedger.EncryptionKey").Protect("existing-key-value");
            await using (var seed = src.CreateCommand(
                "INSERT INTO users (user_id, discord_id, created_at, encryption_key) VALUES ($1, $2, $3, $4)")) {
                seed.Parameters.AddWithValue(userId);
                seed.Parameters.AddWithValue("99999");
                seed.Parameters.AddWithValue(1000L);
                seed.Parameters.AddWithValue(protectedKey);
                await seed.ExecuteNonQueryAsync();
            }

            var key = await endpoints.EnsureEncryptionKeyAsync(userId);

            Assert.Equal("existing-key-value", key);
        } finally {
            await DropSchemaAsync(setupSrc);
        }
    }

    // Regression: SyncKit.Identity mints user_id independently of EggLedger's local table, so a
    // returning user's local row can carry a stale user_id under the same discord_id once
    // identity resolves a different one. StorePending must repoint the existing row (and any
    // el_* rows under the old id) instead of colliding on idx_users_discord_id.
    [SkippableFact]
    public async Task StorePending_repoints_stale_local_user_id_to_identitys_resolved_id() {
        Skip.If(string.IsNullOrEmpty(TestDbUrl), "EGGLEDGER_TEST_DB_URL not set; live Postgres auth test skipped.");

        await using var setupSrc = NpgsqlDataSource.Create(TestDbUrl!);
        await CreateSchemaAsync(setupSrc);

        var scopedBuilder = new NpgsqlConnectionStringBuilder(TestDbUrl!) { SearchPath = Schema };
        await using var src = NpgsqlDataSource.Create(scopedBuilder.ConnectionString);
        try {
            var staleUserId = Guid.NewGuid();
            await using (var seed = src.CreateCommand(
                "INSERT INTO users (user_id, discord_id, created_at, username) VALUES ($1, '12345', $2, 'old-name')")) {
                seed.Parameters.AddWithValue(staleUserId);
                seed.Parameters.AddWithValue(1000L);
                await seed.ExecuteNonQueryAsync();
            }
            await using (var seedMission = src.CreateCommand(
                "INSERT INTO el_mission (user_id, discord_id, player_id, mission_id, start_timestamp, complete_payload) " +
                "VALUES ($1, '12345', 'player-1', 'mission-1', 1000, $2)")) {
                seedMission.Parameters.AddWithValue(staleUserId);
                seedMission.Parameters.AddWithValue(Array.Empty<byte>());
                await seedMission.ExecuteNonQueryAsync();
            }

            var resolvedUserId = Guid.NewGuid();
            var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(HttpStatusCode.OK,
                $$"""{"userId":"{{resolvedUserId}}","role":"viewer","discordId":"12345","isNew":false}"""));
            var identity = new IdentityApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8090") });
            var endpoints = new AuthEndpoints(src, new EphemeralDataProtectionProvider(), identity, NullLogger<AuthEndpoints>.Instance);
            var discordUser = new DiscordUser("12345", "new-name", "");

            var (userId, _) = await endpoints.StorePending("state-1", "token-1", discordUser);

            Assert.Equal(resolvedUserId, userId);

            await using var usersCmd = src.CreateCommand("SELECT user_id, username FROM users WHERE discord_id = '12345'");
            await using var usersReader = await usersCmd.ExecuteReaderAsync();
            Assert.True(await usersReader.ReadAsync());
            Assert.Equal(resolvedUserId, usersReader.GetGuid(0));
            Assert.Equal("new-name", usersReader.GetString(1));
            Assert.False(await usersReader.ReadAsync(), "stale user_id row must not survive alongside the repointed one");
            await usersReader.CloseAsync();

            await using var missionCmd = src.CreateCommand("SELECT user_id FROM el_mission WHERE discord_id = '12345'");
            var missionUserId = Assert.IsType<Guid>(await missionCmd.ExecuteScalarAsync());
            Assert.Equal(resolvedUserId, missionUserId);
        } finally {
            await DropSchemaAsync(setupSrc);
        }
    }

    // The new-user (non-Discord) case: a row with a user_id but no discord_id must still get
    // a key generated and persisted, queryable back out by user_id - this is the actual bug fix,
    // since the old discord_id-keyed lookup could never match such a row.
    [SkippableFact]
    public async Task EnsureEncryptionKeyAsync_generates_and_persists_key_for_non_discord_user() {
        Skip.If(string.IsNullOrEmpty(TestDbUrl), "EGGLEDGER_TEST_DB_URL not set; live Postgres auth test skipped.");

        await using var setupSrc = NpgsqlDataSource.Create(TestDbUrl!);
        await CreateSchemaAsync(setupSrc);

        var scopedBuilder = new NpgsqlConnectionStringBuilder(TestDbUrl!) { SearchPath = Schema };
        await using var src = NpgsqlDataSource.Create(scopedBuilder.ConnectionString);
        try {
            var identity = new IdentityApiClient(new HttpClient(new StubHttpMessageHandler(_ =>
                StubHttpMessageHandler.Json(HttpStatusCode.OK, "{}"))) { BaseAddress = new Uri("http://localhost:8090") });
            var endpoints = new AuthEndpoints(src, new EphemeralDataProtectionProvider(), identity, NullLogger<AuthEndpoints>.Instance);

            var userId = Guid.NewGuid();
            await using (var seed = src.CreateCommand(
                "INSERT INTO users (user_id, discord_id, created_at) VALUES ($1, NULL, $2)")) {
                seed.Parameters.AddWithValue(userId);
                seed.Parameters.AddWithValue(2000L);
                await seed.ExecuteNonQueryAsync();
            }

            var key = await endpoints.EnsureEncryptionKeyAsync(userId);

            Assert.False(string.IsNullOrEmpty(key));

            await using var cmd = src.CreateCommand("SELECT encryption_key FROM users WHERE user_id = $1");
            cmd.Parameters.AddWithValue(userId);
            var stored = Assert.IsType<string>(await cmd.ExecuteScalarAsync());
            Assert.False(string.IsNullOrEmpty(stored));
        } finally {
            await DropSchemaAsync(setupSrc);
        }
    }

    [SkippableFact]
    public async Task SessionFromLogin_Unauthenticated_Returns401() {
        Skip.If(string.IsNullOrEmpty(TestDbUrl), "EGGLEDGER_TEST_DB_URL not set; live Postgres auth test skipped.");

        await using var setupSrc = NpgsqlDataSource.Create(TestDbUrl!);
        await CreateSchemaAsync(setupSrc);

        var scopedBuilder = new NpgsqlConnectionStringBuilder(TestDbUrl!) { SearchPath = Schema };
        await using var src = NpgsqlDataSource.Create(scopedBuilder.ConnectionString);
        try {
            var identity = new IdentityApiClient(new HttpClient(new StubHttpMessageHandler(_ =>
                StubHttpMessageHandler.Json(HttpStatusCode.OK, "{}"))) { BaseAddress = new Uri("http://localhost:8090") });
            var endpoints = new AuthEndpoints(src, new EphemeralDataProtectionProvider(), identity, NullLogger<AuthEndpoints>.Instance);

            var ctx = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };
            ctx.Response.Body = new MemoryStream();

            await endpoints.SessionFromLogin(ctx);

            Assert.Equal(StatusCodes.Status401Unauthorized, ctx.Response.StatusCode);
        } finally {
            await DropSchemaAsync(setupSrc);
        }
    }

    // Covers a pure-Authentik user: no discord_id claim, so sessions.discord_id must be
    // written as NULL and the response must still carry a usable token/encryption key.
    [SkippableFact]
    public async Task SessionFromLogin_AuthenticatedNoDiscordId_CreatesSessionWithNullDiscordId() {
        Skip.If(string.IsNullOrEmpty(TestDbUrl), "EGGLEDGER_TEST_DB_URL not set; live Postgres auth test skipped.");

        await using var setupSrc = NpgsqlDataSource.Create(TestDbUrl!);
        await CreateSchemaAsync(setupSrc);

        var scopedBuilder = new NpgsqlConnectionStringBuilder(TestDbUrl!) { SearchPath = Schema };
        await using var src = NpgsqlDataSource.Create(scopedBuilder.ConnectionString);
        try {
            var identity = new IdentityApiClient(new HttpClient(new StubHttpMessageHandler(_ =>
                StubHttpMessageHandler.Json(HttpStatusCode.OK, "{}"))) { BaseAddress = new Uri("http://localhost:8090") });
            var endpoints = new AuthEndpoints(src, new EphemeralDataProtectionProvider(), identity, NullLogger<AuthEndpoints>.Instance);

            var userId = Guid.NewGuid();
            await using (var seed = src.CreateCommand(
                "INSERT INTO users (user_id, discord_id, created_at) VALUES ($1, NULL, $2)")) {
                seed.Parameters.AddWithValue(userId);
                seed.Parameters.AddWithValue(3000L);
                await seed.ExecuteNonQueryAsync();
            }

            var claims = new List<Claim> {
                new(EggLedger.Web.Server.Auth.AuthScheme.UserIdClaim, userId.ToString()),
                new(ClaimTypes.Name, "authentik-user"),
            };
            var ctx = new DefaultHttpContext {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, EggLedger.Web.Server.Auth.AuthScheme.Cookie)),
            };
            ctx.Response.Body = new MemoryStream();

            await endpoints.SessionFromLogin(ctx);

            Assert.Equal(StatusCodes.Status200OK, ctx.Response.StatusCode);
            ctx.Response.Body.Seek(0, SeekOrigin.Begin);
            var body = await JsonSerializer.DeserializeAsync<PollResponse>(ctx.Response.Body, JsonOptions);
            Assert.NotNull(body);
            Assert.False(string.IsNullOrEmpty(body!.Token));

            await using var cmd = src.CreateCommand(
                "SELECT user_id, discord_id FROM sessions WHERE token = $1");
            cmd.Parameters.AddWithValue(body.Token);
            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(userId, reader.GetGuid(0));
            Assert.True(reader.IsDBNull(1));
        } finally {
            await DropSchemaAsync(setupSrc);
        }
    }

    // Covers a Discord-linked user hitting the endpoint from a cookie session: the
    // discord_id claim must flow through into sessions.discord_id, not be dropped.
    [SkippableFact]
    public async Task SessionFromLogin_AuthenticatedWithDiscordId_CreatesSessionWithDiscordId() {
        Skip.If(string.IsNullOrEmpty(TestDbUrl), "EGGLEDGER_TEST_DB_URL not set; live Postgres auth test skipped.");

        await using var setupSrc = NpgsqlDataSource.Create(TestDbUrl!);
        await CreateSchemaAsync(setupSrc);

        var scopedBuilder = new NpgsqlConnectionStringBuilder(TestDbUrl!) { SearchPath = Schema };
        await using var src = NpgsqlDataSource.Create(scopedBuilder.ConnectionString);
        try {
            var identity = new IdentityApiClient(new HttpClient(new StubHttpMessageHandler(_ =>
                StubHttpMessageHandler.Json(HttpStatusCode.OK, "{}"))) { BaseAddress = new Uri("http://localhost:8090") });
            var endpoints = new AuthEndpoints(src, new EphemeralDataProtectionProvider(), identity, NullLogger<AuthEndpoints>.Instance);

            var userId = Guid.NewGuid();
            await using (var seed = src.CreateCommand(
                "INSERT INTO users (user_id, discord_id, created_at, avatar_url) VALUES ($1, $2, $3, $4)")) {
                seed.Parameters.AddWithValue(userId);
                seed.Parameters.AddWithValue("54321");
                seed.Parameters.AddWithValue(4000L);
                seed.Parameters.AddWithValue("http://example.com/avatar.png");
                await seed.ExecuteNonQueryAsync();
            }

            var claims = new List<Claim> {
                new(EggLedger.Web.Server.Auth.AuthScheme.UserIdClaim, userId.ToString()),
                new(EggLedger.Web.Server.Auth.AuthScheme.DiscordIdClaim, "54321"),
                new(ClaimTypes.Name, "discord-user"),
            };
            var ctx = new DefaultHttpContext {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, EggLedger.Web.Server.Auth.AuthScheme.Cookie)),
            };
            ctx.Response.Body = new MemoryStream();

            await endpoints.SessionFromLogin(ctx);

            Assert.Equal(StatusCodes.Status200OK, ctx.Response.StatusCode);
            ctx.Response.Body.Seek(0, SeekOrigin.Begin);
            var body = await JsonSerializer.DeserializeAsync<PollResponse>(ctx.Response.Body, JsonOptions);
            Assert.NotNull(body);
            Assert.Equal("http://example.com/avatar.png", body!.AvatarUrl);

            await using var cmd = src.CreateCommand(
                "SELECT user_id, discord_id FROM sessions WHERE token = $1");
            cmd.Parameters.AddWithValue(body.Token);
            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(userId, reader.GetGuid(0));
            Assert.Equal("54321", reader.GetString(1));
        } finally {
            await DropSchemaAsync(setupSrc);
        }
    }

    [SkippableFact]
    public async Task DeleteSession_calls_identity_revoke() {
        Skip.If(string.IsNullOrEmpty(TestDbUrl), "EGGLEDGER_TEST_DB_URL not set; live Postgres auth test skipped.");

        await using var setupSrc = NpgsqlDataSource.Create(TestDbUrl!);
        await CreateSchemaAsync(setupSrc);

        var scopedBuilder = new NpgsqlConnectionStringBuilder(TestDbUrl!) { SearchPath = Schema };
        await using var src = NpgsqlDataSource.Create(scopedBuilder.ConnectionString);
        try {
            const string token = "tok-to-revoke";
            HttpRequestMessage? revokeRequest = null;
            var handler = new StubHttpMessageHandler(req => {
                revokeRequest = req;
                return StubHttpMessageHandler.Json(HttpStatusCode.OK, "{}");
            });
            var identity = new IdentityApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8090") });
            var endpoints = new AuthEndpoints(src, new EphemeralDataProtectionProvider(), identity, NullLogger<AuthEndpoints>.Instance);

            var userId = Guid.NewGuid();
            await using (var seed = src.CreateCommand(
                "INSERT INTO users (user_id, discord_id, created_at) VALUES ($1, NULL, $2);" +
                "INSERT INTO sessions (token, discord_id, user_id, expires_at) VALUES ($3, NULL, $1, $4)")) {
                seed.Parameters.AddWithValue(userId);
                seed.Parameters.AddWithValue(3000L);
                seed.Parameters.AddWithValue(token);
                seed.Parameters.AddWithValue(DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds());
                await seed.ExecuteNonQueryAsync();
            }

            var ctx = new DefaultHttpContext();
            ctx.Request.Headers.Authorization = $"Bearer {token}";
            ctx.Response.Body = new MemoryStream();

            await endpoints.DeleteSession(ctx);

            Assert.Equal(StatusCodes.Status204NoContent, ctx.Response.StatusCode);
            Assert.NotNull(revokeRequest);
            Assert.Equal("/identity/revoke-session", revokeRequest!.RequestUri!.AbsolutePath);
            var body = await revokeRequest.Content!.ReadAsStringAsync();
            Assert.Contains(token, body);

            await using var cmd = src.CreateCommand("SELECT COUNT(*) FROM sessions WHERE token = $1");
            cmd.Parameters.AddWithValue(token);
            Assert.Equal(0L, await cmd.ExecuteScalarAsync());
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
        await ApplyMigrationAsync(src, "7_cascade_eggledger_storage.up.sql");
        await ApplyMigrationAsync(src, "8_identities.up.sql");
        await ApplyMigrationAsync(src, "9_identity_user_id_cascade.up.sql");
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
