using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Npgsql;

namespace EggLedger.Web.Server.Sync.Admin;

// Admin-only management API, gated by the ADMIN_DISCORD_IDS allowlist. Every
// handler runs behind RequireAuth (sets X-Discord-ID, which now carries the
// provider-neutral user_id string post-migration; header name is unchanged).
public sealed class AdminEndpoints(NpgsqlDataSource source, ApiMetrics metrics, SpamLog spam, IReadOnlySet<string> adminIds) {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static string UserId(HttpContext ctx) => ctx.Request.Headers["X-Discord-ID"].ToString();
    private bool IsAdmin(HttpContext ctx) => adminIds.Contains(UserId(ctx));

    private static async Task ForbidAsync(HttpContext ctx) {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        await JsonSerializer.SerializeAsync(ctx.Response.Body, new { error = "admin role required" }, Json, ctx.RequestAborted);
    }

    private static async Task JsonAsync<T>(HttpContext ctx, T value) {
        ctx.Response.StatusCode = StatusCodes.Status200OK;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        await JsonSerializer.SerializeAsync(ctx.Response.Body, value, Json, ctx.RequestAborted);
    }

    // GET /api/v1/admin/me -> { isAdmin }. Lets the client show/hide the Admin tab.
    public async Task Me(HttpContext ctx) =>
        await JsonAsync(ctx, new { isAdmin = IsAdmin(ctx) });

    public async Task Users(HttpContext ctx) {
        if (!IsAdmin(ctx)) { await ForbidAsync(ctx); return; }
        var users = new List<object>();
        await using var cmd = source.CreateCommand(
            "SELECT u.discord_id, u.username, u.avatar_url, u.user_id, " +
            "(SELECT COUNT(*) FROM blobs b WHERE b.user_id = u.user_id) AS blob_count, " +
            "(SELECT COALESCE(SUM(octet_length(b.ciphertext)), 0) FROM blobs b WHERE b.user_id = u.user_id) AS storage_bytes, " +
            "(SELECT MAX(expires_at) FROM sessions s WHERE s.user_id = u.user_id) AS last_session " +
            "FROM users u ORDER BY u.username");
        await using var reader = await cmd.ExecuteReaderAsync(ctx.RequestAborted);
        while (await reader.ReadAsync(ctx.RequestAborted)) {
            users.Add(new {
                discordId = reader.IsDBNull(0) ? "" : reader.GetString(0),
                username = reader.GetString(1),
                avatarUrl = reader.GetString(2),
                userId = reader.GetGuid(3),
                blobCount = reader.GetInt64(4),
                storageBytes = reader.GetInt64(5),
                lastSession = reader.IsDBNull(6) ? (long?)null : reader.GetInt64(6),
                isAdmin = !reader.IsDBNull(0) && adminIds.Contains(reader.GetString(0)),
            });
        }
        await JsonAsync(ctx, users);
    }

    // GET /api/v1/admin/metrics -> per-minute request totals + per-path tallies.
    public async Task Metrics(HttpContext ctx) {
        if (!IsAdmin(ctx)) { await ForbidAsync(ctx); return; }
        await JsonAsync(ctx, new {
            minutes = metrics.SnapshotMinutes(),
            paths = metrics.SnapshotPaths(),
            spam = await spam.SnapshotAsync(),
        });
    }

    // DELETE /api/v1/admin/users/{userId}: remove a user (cascades sessions + blobs).
    public async Task DeleteUser(HttpContext ctx, string userId) {
        if (!IsAdmin(ctx)) { await ForbidAsync(ctx); return; }
        if (!Guid.TryParse(userId, out var parsedUserId)) {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await JsonAsync(ctx, new { error = "invalid user id" });
            return;
        }
        if (userId == UserId(ctx)) {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await JsonAsync(ctx, new { error = "cannot delete your own account" });
            return;
        }
        await using var cmd = source.CreateCommand("DELETE FROM users WHERE user_id = $1");
        cmd.Parameters.AddWithValue(parsedUserId);
        var rows = await cmd.ExecuteNonQueryAsync(ctx.RequestAborted);
        await JsonAsync(ctx, new { deleted = rows > 0 });
    }
}
