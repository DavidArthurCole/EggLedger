using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Npgsql;
using SyncKit.Auth;
using SyncKit.Contract;
using SyncKit.Identity.Client;

namespace EggLedger.Web.Server.Sync.Auth;

// The original Go server swallowed DB errors on these auth writes (fire-and-forget); the catch
// blocks keep that. The callback adds a state check the Go version lacked, to block login CSRF.

public sealed record DiscordInitResponse(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("state")] string State);

public sealed record PollResponse(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("avatarUrl")] string AvatarUrl,
    [property: JsonPropertyName("encryptionKey")] string EncryptionKey);

public sealed record RedeemCodeRequest([property: JsonPropertyName("code")] string Code);

public sealed class AuthEndpoints(NpgsqlDataSource source, IDataProtectionProvider dataProtection, IdentityApiClient identity, ILogger<AuthEndpoints> logger) {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly string[] ElUserTables = [
        "el_mission", "el_backup", "el_artifact_drops", "el_settings", "el_reports", "el_report_groups",
    ];
    private readonly IDataProtector _keyProtector = dataProtection.CreateProtector("EggLedger.EncryptionKey");
    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    // Tolerates rows written before this protector existed (plain 64-char hex): unprotect
    // fails on those, so fall back to the raw stored value instead of losing the key.
    private string UnprotectKey(string stored) {
        try {
            return _keyProtector.Unprotect(stored);
        } catch (CryptographicException) {
            return stored;
        }
    }

    private static async Task WriteTextAsync(HttpContext ctx, int statusCode, string text) {
        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = "text/plain; charset=utf-8";
        await ctx.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(text), ctx.RequestAborted);
    }

    private static async Task WriteJsonAsync<T>(HttpContext ctx, T value) {
        ctx.Response.StatusCode = StatusCodes.Status200OK;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        await JsonSerializer.SerializeAsync(ctx.Response.Body, value, Json, ctx.RequestAborted);
    }

    public async Task Discord(HttpContext ctx) {
        var (url, state) = await BeginAuthAsync(ctx).ConfigureAwait(false);
        await WriteJsonAsync(ctx, new DiscordInitResponse(url, state));
    }

    // Browser-friendly entry: 302 straight to Discord. The ConnectGate links here with a
    // plain anchor, so no Blazor-circuit JS-interop navigation (which spammed render/cancel
    // errors when the circuit tore down mid-redirect).
    public async Task Login(HttpContext ctx) {
        var (url, _) = await BeginAuthAsync(ctx).ConfigureAwait(false);
        ctx.Response.Redirect(url);
    }

    private async Task<(string Url, string State)> BeginAuthAsync(HttpContext ctx) {
        var (url, state) = DiscordOAuth.AuthUrl();
        await using var cmd = source.CreateCommand(
            "INSERT INTO pending_auth (state, expires_at) VALUES ($1, $2) ON CONFLICT (state) DO UPDATE SET expires_at = EXCLUDED.expires_at");
        cmd.Parameters.AddWithValue(state);
        cmd.Parameters.AddWithValue(Now() + 600);
        try { await cmd.ExecuteNonQueryAsync(ctx.RequestAborted); } catch (Exception ex) { logger.LogWarning(ex, "auth: failed to seed pending_auth row"); }
        return (url, state);
    }

    public async Task Callback(HttpContext ctx) {
        var code = ctx.Request.Query["code"].ToString();
        var state = ctx.Request.Query["state"].ToString();

        // Reject a state the server never issued, so a forged callback cannot sign a session
        // cookie into an attacker-chosen account (login CSRF). BeginAuthAsync seeds the row.
        if (string.IsNullOrEmpty(state) || !await StateIsPending(state, ctx.RequestAborted)) {
            await WriteTextAsync(ctx, StatusCodes.Status400BadRequest, "invalid or expired state\n");
            return;
        }

        DiscordUser? authedUser = null;
        (Guid UserId, string Role)? resolved = null;
        try {
            await DiscordOAuth.HandleCallbackAsync(
                code, state,
                async (s, token, user) => { authedUser = user; resolved = await StorePending(s, token, user); },
                ctx.RequestAborted);
        } catch (Exception ex) {
            logger.LogWarning(ex, "auth: Discord callback failed");
            await WriteTextAsync(ctx, StatusCodes.Status500InternalServerError, "auth failed\n");
            return;
        }

        // Sign the cookie-auth principal carrying both the legacy Discord id and the
        // provider-neutral user id/role; the framework AuthenticationStateProvider flows it
        // into the Blazor circuit (server cookie session).
        if (authedUser is { } u) {
            var claims = new List<Claim> {
                new(EggLedger.Web.Server.Auth.AuthScheme.DiscordIdClaim, u.Id),
                new(ClaimTypes.Name, u.Username),
            };
            if (resolved is { } r) {
                claims.Add(new Claim(EggLedger.Web.Server.Auth.AuthScheme.UserIdClaim, r.UserId.ToString()));
                claims.Add(new Claim(EggLedger.Web.Server.Auth.AuthScheme.RoleClaim, r.Role));
            }
            var identity = new ClaimsIdentity(claims, EggLedger.Web.Server.Auth.AuthScheme.Cookie);
            await ctx.SignInAsync(
                EggLedger.Web.Server.Auth.AuthScheme.Cookie,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = true });
        }

        ctx.Response.StatusCode = StatusCodes.Status200OK;
        ctx.Response.ContentType = "text/html; charset=utf-8";
        await ctx.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(SuccessPage.Html), ctx.RequestAborted);
    }

    // Embedded popup widget: exchanges a short-lived SyncKit login code for this app's own
    // cookie. Covers both Discord- and Authentik-resolved identities (RedeemLoginCodeResponse
    // carries whichever provider actually authenticated), unlike the Discord-only Callback path.
    public async Task RedeemCode(HttpContext ctx) {
        RedeemCodeRequest? body;
        try {
            body = await JsonSerializer.DeserializeAsync<RedeemCodeRequest>(ctx.Request.Body, Json, ctx.RequestAborted);
        } catch (JsonException) {
            await WriteTextAsync(ctx, StatusCodes.Status400BadRequest, "invalid body\n");
            return;
        }
        if (string.IsNullOrWhiteSpace(body?.Code)) {
            await WriteTextAsync(ctx, StatusCodes.Status400BadRequest, "missing code\n");
            return;
        }

        RedeemLoginCodeResponse result;
        try {
            result = await identity.RedeemAsync(body.Code, ctx.RequestAborted);
        } catch (HttpRequestException ex) {
            logger.LogWarning(ex, "auth: redeem-code failed");
            await WriteTextAsync(ctx, StatusCodes.Status400BadRequest, "redeem failed\n");
            return;
        }

        await UpsertLocalUserAsync(result.UserId, result.DiscordId, result.Username, result.Avatar, ctx.RequestAborted);

        var claims = new List<Claim> {
            new(ClaimTypes.Name, result.Username),
            new(EggLedger.Web.Server.Auth.AuthScheme.UserIdClaim, result.UserId.ToString()),
            new(EggLedger.Web.Server.Auth.AuthScheme.RoleClaim, result.Role),
        };
        if (!string.IsNullOrEmpty(result.DiscordId)) {
            claims.Add(new Claim(EggLedger.Web.Server.Auth.AuthScheme.DiscordIdClaim, result.DiscordId));
        }
        var claimsIdentity = new ClaimsIdentity(claims, EggLedger.Web.Server.Auth.AuthScheme.Cookie);
        await ctx.SignInAsync(
            EggLedger.Web.Server.Auth.AuthScheme.Cookie,
            new ClaimsPrincipal(claimsIdentity),
            new AuthenticationProperties { IsPersistent = true });

        await WriteJsonAsync(ctx, new { discordId = result.DiscordId, username = result.Username, avatar = result.Avatar });
    }

    // Mirrors StorePending's repoint-then-upsert for the local users row, minus the
    // sessions/pending_auth bookkeeping that only the bearer-token Discord OAuth path needs.
    private async Task UpsertLocalUserAsync(Guid userId, string? discordId, string? username, string? avatar, CancellationToken ct) {
        if (!string.IsNullOrEmpty(discordId)) {
            await using var repoint = source.CreateCommand(
                "UPDATE users SET user_id = $1 WHERE discord_id = $2 AND user_id <> $1");
            repoint.Parameters.AddWithValue(userId);
            repoint.Parameters.AddWithValue(discordId);
            if (await repoint.ExecuteNonQueryAsync(ct) > 0) {
                foreach (var table in ElUserTables) {
                    await using var move = source.CreateCommand(
                        $"UPDATE {table} SET user_id = $1 WHERE discord_id = $2 AND user_id <> $1");
                    move.Parameters.AddWithValue(userId);
                    move.Parameters.AddWithValue(discordId);
                    await move.ExecuteNonQueryAsync(ct);
                }
            }
        }

        await using (var u = source.CreateCommand(
            "INSERT INTO users (user_id, discord_id, created_at, username, avatar_url) VALUES ($1,$2,$3,$4,$5) " +
            "ON CONFLICT (user_id) DO UPDATE SET username = EXCLUDED.username, avatar_url = EXCLUDED.avatar_url")) {
            u.Parameters.AddWithValue(userId);
            if (string.IsNullOrEmpty(discordId)) {
                u.Parameters.AddWithValue(DBNull.Value);
            } else {
                u.Parameters.AddWithValue(discordId);
            }
            u.Parameters.AddWithValue(Now());
            u.Parameters.AddWithValue(username ?? "");
            u.Parameters.AddWithValue(avatar ?? "");
            await u.ExecuteNonQueryAsync(ct);
        }

        await EnsureEncryptionKeyAsync(userId);
    }

    private async Task<bool> StateIsPending(string state, CancellationToken ct) {
        await using var cmd = source.CreateCommand(
            "SELECT 1 FROM pending_auth WHERE state = $1 AND expires_at > $2");
        cmd.Parameters.AddWithValue(state);
        cmd.Parameters.AddWithValue(Now());
        try {
            return await cmd.ExecuteScalarAsync(ct) is not null;
        } catch (Exception ex) {
            logger.LogWarning(ex, "auth: failed to check pending state");
            return false;
        }
    }

    // Keyed by user_id (the table's real PK since migration 8), not discord_id, so it also
    // works for non-Discord identities whose row has no discord_id at all.
    internal async Task<string> EnsureEncryptionKeyAsync(Guid userId) {
        var encKey = "";
        await using (var sel = source.CreateCommand("SELECT encryption_key FROM users WHERE user_id = $1")) {
            sel.Parameters.AddWithValue(userId);
            // On error encKey stays empty and is regenerated below (Go parity).
            try {
                var result = await sel.ExecuteScalarAsync();
                if (result is string { Length: > 0 } stored)
                    encKey = UnprotectKey(stored);
            } catch (Exception ex) {
                logger.LogWarning(ex, "auth: failed to read encryption_key for {UserId}", userId);
            }
        }
        if (string.IsNullOrEmpty(encKey)) {
            encKey = DiscordOAuth.GenerateEncryptionKey();
            await using var upd = source.CreateCommand("UPDATE users SET encryption_key = $1 WHERE user_id = $2");
            upd.Parameters.AddWithValue(_keyProtector.Protect(encKey));
            upd.Parameters.AddWithValue(userId);
            try { await upd.ExecuteNonQueryAsync(); } catch (Exception ex) { logger.LogWarning(ex, "auth: failed to store encryption_key for {UserId}", userId); }
        }
        return encKey;
    }

    public async Task<(Guid UserId, string Role)> StorePending(string state, string token, DiscordUser user) {
        var resolved = await identity.ResolveAsync("discord", user.Id, user.Id, user.Username, user.AvatarUrl, CancellationToken.None);
        var userId = resolved.UserId;

        // Identity's user_id space is independent of EggLedger's own (seeded separately at
        // cutover), so a returning user's local row can still carry a stale user_id under this
        // discord_id. Repoint it to identity's authoritative value first (cascades to
        // sessions/blobs via migration 9's FK, el_* tables have no FK so get updated explicitly)
        // so the upsert below never collides on idx_users_discord_id.
        await using (var repoint = source.CreateCommand(
            "UPDATE users SET user_id = $1 WHERE discord_id = $2 AND user_id <> $1")) {
            repoint.Parameters.AddWithValue(userId);
            repoint.Parameters.AddWithValue(user.Id);
            if (await repoint.ExecuteNonQueryAsync() > 0) {
                foreach (var table in ElUserTables) {
                    await using var move = source.CreateCommand(
                        $"UPDATE {table} SET user_id = $1 WHERE discord_id = $2 AND user_id <> $1");
                    move.Parameters.AddWithValue(userId);
                    move.Parameters.AddWithValue(user.Id);
                    await move.ExecuteNonQueryAsync();
                }
            }
        }

        // The local users row (username/avatar_url/encryption_key) is still EggLedger app data
        // until the post-verification migration drops the identity columns here; user_id now
        // comes from SyncKit.Identity above rather than a local INSERT ... RETURNING.
        await using (var u = source.CreateCommand(
            "INSERT INTO users (user_id, discord_id, created_at, username, avatar_url) VALUES ($1,$2,$3,$4,$5) " +
            "ON CONFLICT (user_id) DO UPDATE SET username = EXCLUDED.username, avatar_url = EXCLUDED.avatar_url")) {
            u.Parameters.AddWithValue(userId);
            u.Parameters.AddWithValue(user.Id);
            u.Parameters.AddWithValue(Now());
            u.Parameters.AddWithValue(user.Username);
            u.Parameters.AddWithValue(user.AvatarUrl);
            await u.ExecuteNonQueryAsync();
        }

        var encKey = await EnsureEncryptionKeyAsync(userId);

        await using (var ins = source.CreateCommand(
            "INSERT INTO sessions (token, discord_id, user_id, expires_at) VALUES ($1,$2,$3,$4)")) {
            ins.Parameters.AddWithValue(token);
            ins.Parameters.AddWithValue(user.Id);
            ins.Parameters.AddWithValue(userId);
            ins.Parameters.AddWithValue(Now() + 30L * 24 * 3600);
            await ins.ExecuteNonQueryAsync();
        }

        await using var pend = source.CreateCommand(
            "UPDATE pending_auth SET session_token = $1, username = $2, avatar_url = $3, encryption_key = $4 WHERE state = $5");
        pend.Parameters.AddWithValue(token);
        pend.Parameters.AddWithValue(user.Username);
        pend.Parameters.AddWithValue(user.AvatarUrl);
        pend.Parameters.AddWithValue(_keyProtector.Protect(encKey));
        pend.Parameters.AddWithValue(state);
        await pend.ExecuteNonQueryAsync();

        return (userId, resolved.Role);
    }

    public async Task Poll(HttpContext ctx, string state) {
        string? token = null;
        long expiresAt = 0;
        string username = "", avatarUrl = "", encryptionKey = "";
        var found = false;
        // On error, found stays false and the caller returns 404 (Go parity).
        try {
            await using var cmd = source.CreateCommand(
                "SELECT session_token, expires_at, username, avatar_url, encryption_key FROM pending_auth WHERE state = $1");
            cmd.Parameters.AddWithValue(state);
            await using var reader = await cmd.ExecuteReaderAsync(ctx.RequestAborted);
            if (await reader.ReadAsync(ctx.RequestAborted)) {
                found = true;
                token = reader.IsDBNull(0) ? null : reader.GetString(0);
                expiresAt = reader.GetInt64(1);
                username = reader.IsDBNull(2) ? "" : reader.GetString(2);
                avatarUrl = reader.IsDBNull(3) ? "" : reader.GetString(3);
                encryptionKey = reader.IsDBNull(4) ? "" : reader.GetString(4);
            }
        } catch (Exception ex) {
            logger.LogWarning(ex, "auth: poll query failed for state {State}", state);
        }

        if (!found || Now() > expiresAt) {
            await WriteTextAsync(ctx, StatusCodes.Status404NotFound, "not found\n");
            return;
        }
        if (token is null) {
            ctx.Response.StatusCode = StatusCodes.Status202Accepted;
            return;
        }
        await using (var del = source.CreateCommand("DELETE FROM pending_auth WHERE state = $1")) {
            del.Parameters.AddWithValue(state);
            try { await del.ExecuteNonQueryAsync(ctx.RequestAborted); } catch (Exception ex) { logger.LogWarning(ex, "auth: failed to delete pending_auth row for state {State}", state); }
        }
        var plainKey = string.IsNullOrEmpty(encryptionKey) ? "" : UnprotectKey(encryptionKey);
        await WriteJsonAsync(ctx, new PollResponse(token, username, avatarUrl, plainKey));
    }

    // Mints a sync session directly from the caller's authenticated cookie identity (Blazor
    // Server login), skipping the Discord OAuth handshake entirely - for Web, where the user
    // is already logged in via whatever provider (Discord or Authentik) gated the app itself.
    public async Task SessionFromLogin(HttpContext ctx) {
        if (ctx.User.Identity?.IsAuthenticated != true) {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var userIdClaim = ctx.User.FindFirst(EggLedger.Web.Server.Auth.AuthScheme.UserIdClaim)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId)) {
            await WriteTextAsync(ctx, StatusCodes.Status401Unauthorized, "no resolved user identity\n");
            return;
        }

        var username = ctx.User.Identity?.Name ?? "";
        var discordIdClaim = ctx.User.FindFirst(EggLedger.Web.Server.Auth.AuthScheme.DiscordIdClaim)?.Value;

        var encKey = await EnsureEncryptionKeyAsync(userId);
        var token = Guid.NewGuid().ToString("N");

        await using (var ins = source.CreateCommand(
            "INSERT INTO sessions (token, discord_id, user_id, expires_at) VALUES ($1, $2, $3, $4)")) {
            ins.Parameters.AddWithValue(token);
            if (string.IsNullOrEmpty(discordIdClaim)) {
                ins.Parameters.AddWithValue(DBNull.Value);
            } else {
                ins.Parameters.AddWithValue(discordIdClaim);
            }
            ins.Parameters.AddWithValue(userId);
            ins.Parameters.AddWithValue(Now() + 30L * 24 * 3600);
            try {
                await ins.ExecuteNonQueryAsync(ctx.RequestAborted);
            } catch (Exception ex) {
                logger.LogWarning(ex, "auth: failed to create session-from-login for {UserId}", userId);
                await WriteTextAsync(ctx, StatusCodes.Status500InternalServerError, "internal error\n");
                return;
            }
        }

        var avatarUrl = await UserAvatarUrlAsync(userId, ctx.RequestAborted);
        await WriteJsonAsync(ctx, new PollResponse(token, username, avatarUrl, encKey));
    }

    // Discord-linked identity carries an avatar synced from the last Discord OAuth login;
    // non-Discord users have none, and CloudSyncPanel already renders an empty avatar gracefully.
    private async Task<string> UserAvatarUrlAsync(Guid userId, CancellationToken ct) {
        await using var cmd = source.CreateCommand("SELECT avatar_url FROM users WHERE user_id = $1");
        cmd.Parameters.AddWithValue(userId);
        try {
            var result = await cmd.ExecuteScalarAsync(ct);
            return result as string ?? "";
        } catch (Exception ex) {
            logger.LogWarning(ex, "auth: failed to read avatar_url for {UserId}", userId);
            return "";
        }
    }

    public async Task DeleteSession(HttpContext ctx) {
        var authHeader = ctx.Request.Headers["Authorization"].ToString();
        var token = authHeader.StartsWith("Bearer ", StringComparison.Ordinal)
            ? authHeader["Bearer ".Length..]
            : "";
        if (token.Length > 0) {
            await using var cmd = source.CreateCommand("DELETE FROM sessions WHERE token = $1");
            cmd.Parameters.AddWithValue(token);
            try { await cmd.ExecuteNonQueryAsync(ctx.RequestAborted); } catch (Exception ex) { logger.LogWarning(ex, "auth: failed to delete session"); }
            try { await identity.RevokeSessionAsync(token, ctx.RequestAborted); } catch (Exception ex) { logger.LogWarning(ex, "auth: failed to revoke session"); }
        }
        ctx.Response.StatusCode = StatusCodes.Status204NoContent;
    }
}
