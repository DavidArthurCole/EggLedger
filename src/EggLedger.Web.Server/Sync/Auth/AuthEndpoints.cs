using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Npgsql;
using Synckit.Auth;

namespace EggLedger.Web.Server.Sync.Auth;

public sealed record DiscordInitResponse(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("state")] string State);

public sealed record PollResponse(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("avatarUrl")] string AvatarUrl,
    [property: JsonPropertyName("encryptionKey")] string EncryptionKey);

public sealed class AuthEndpoints(NpgsqlDataSource source) {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

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
        try { await cmd.ExecuteNonQueryAsync(ctx.RequestAborted); } catch { /* match Go fire-and-forget */ }
        return (url, state);
    }

    public async Task Callback(HttpContext ctx) {
        var code = ctx.Request.Query["code"].ToString();
        var state = ctx.Request.Query["state"].ToString();
        DiscordUser? authedUser = null;
        try {
            await DiscordOAuth.HandleCallbackAsync(
                code, state,
                (s, token, user) => { authedUser = user; return StorePending(s, token, user); },
                ctx.RequestAborted);
        } catch {
            await WriteTextAsync(ctx, StatusCodes.Status500InternalServerError, "auth failed\n");
            return;
        }

        // Sign the cookie-auth principal carrying the Discord id; the framework
        // AuthenticationStateProvider flows it into the Blazor circuit (server cookie session).
        if (authedUser is { } u) {
            var claims = new List<Claim> {
                new(EggLedger.Web.Server.Auth.AuthScheme.DiscordIdClaim, u.Id),
                new(ClaimTypes.Name, u.Username),
            };
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

    public async Task StorePending(string state, string token, DiscordUser user) {
        await using (var u = source.CreateCommand(
            "INSERT INTO users (discord_id, created_at, username, avatar_url) VALUES ($1,$2,$3,$4) " +
            "ON CONFLICT (discord_id) DO UPDATE SET username = EXCLUDED.username, avatar_url = EXCLUDED.avatar_url")) {
            u.Parameters.AddWithValue(user.Id);
            u.Parameters.AddWithValue(Now());
            u.Parameters.AddWithValue(user.Username);
            u.Parameters.AddWithValue(user.AvatarUrl);
            try { await u.ExecuteNonQueryAsync(); } catch { /* match Go: error ignored */ }
        }

        string encKey = "";
        await using (var sel = source.CreateCommand("SELECT encryption_key FROM users WHERE discord_id = $1")) {
            sel.Parameters.AddWithValue(user.Id);
            try {
                var result = await sel.ExecuteScalarAsync();
                if (result is string s)
                    encKey = s;
            } catch { /* match Go: Scan error ignored, encKey stays empty -> regenerate */ }
        }
        if (string.IsNullOrEmpty(encKey)) {
            encKey = DiscordOAuth.GenerateEncryptionKey();
            await using var upd = source.CreateCommand("UPDATE users SET encryption_key = $1 WHERE discord_id = $2");
            upd.Parameters.AddWithValue(encKey);
            upd.Parameters.AddWithValue(user.Id);
            try { await upd.ExecuteNonQueryAsync(); } catch { /* match Go: error ignored */ }
        }

        await using (var ins = source.CreateCommand(
            "INSERT INTO sessions (token, discord_id, expires_at) VALUES ($1,$2,$3)")) {
            ins.Parameters.AddWithValue(token);
            ins.Parameters.AddWithValue(user.Id);
            ins.Parameters.AddWithValue(Now() + 30L * 24 * 3600);
            await ins.ExecuteNonQueryAsync();
        }

        await using var pend = source.CreateCommand(
            "UPDATE pending_auth SET session_token = $1, username = $2, avatar_url = $3, encryption_key = $4 WHERE state = $5");
        pend.Parameters.AddWithValue(token);
        pend.Parameters.AddWithValue(user.Username);
        pend.Parameters.AddWithValue(user.AvatarUrl);
        pend.Parameters.AddWithValue(encKey);
        pend.Parameters.AddWithValue(state);
        await pend.ExecuteNonQueryAsync();
    }

    public async Task Poll(HttpContext ctx, string state) {
        string? token = null;
        long expiresAt = 0;
        string username = "", avatarUrl = "", encryptionKey = "";
        var found = false;
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
        } catch { /* treat as not found, match Go */ }

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
            try { await del.ExecuteNonQueryAsync(ctx.RequestAborted); } catch { /* match Go fire-and-forget */ }
        }
        await WriteJsonAsync(ctx, new PollResponse(token, username, avatarUrl, encryptionKey));
    }

    public async Task DeleteSession(HttpContext ctx) {
        var authHeader = ctx.Request.Headers["Authorization"].ToString();
        var token = authHeader.StartsWith("Bearer ", StringComparison.Ordinal)
            ? authHeader["Bearer ".Length..]
            : "";
        if (token.Length > 0) {
            await using var cmd = source.CreateCommand("DELETE FROM sessions WHERE token = $1");
            cmd.Parameters.AddWithValue(token);
            try { await cmd.ExecuteNonQueryAsync(ctx.RequestAborted); } catch { /* match Go fire-and-forget */ }
        }
        ctx.Response.StatusCode = StatusCodes.Status204NoContent;
    }
}
