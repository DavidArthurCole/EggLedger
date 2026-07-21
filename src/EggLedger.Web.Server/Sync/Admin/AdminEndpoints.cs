using System.Text.Json;
using EggLedger.Web.Server.Sync.Auth;
using Microsoft.AspNetCore.Http;
using Npgsql;
using SyncKit.Identity.Client;

namespace EggLedger.Web.Server.Sync.Admin;

// Admin-only management API, gated by role (SyncKit.Identity's role model, replacing the old
// flat ADMIN_DISCORD_IDS allowlist check). Every handler runs behind RequireAuth (sets
// X-Discord-ID, which now carries the provider-neutral user_id string post-migration; header
// name is unchanged).
public sealed class AdminEndpoints(NpgsqlDataSource source, ApiMetrics metrics, SpamLog spam, ICurrentUser currentUser, IdentityApiClient identity) {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static string UserId(HttpContext ctx) => ctx.Request.Headers["X-Discord-ID"].ToString();
    private Task<bool> IsAdminAsync(HttpContext ctx) => currentUser.IsAtLeastAsync(ctx, UserRole.Admin, ctx.RequestAborted);

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
        await JsonAsync(ctx, new { isAdmin = await IsAdminAsync(ctx) });

    public async Task Users(HttpContext ctx) {
        if (!await IsAdminAsync(ctx)) { await ForbidAsync(ctx); return; }

        // Role now lives in SyncKit.Identity, not a local CSV allowlist - one batch call here
        // (not one per row) keeps this endpoint's own admin-flag agreeing with IsAdminAsync's
        // gate above, since both now derive from the same source.
        var roleByUserId = (await identity.ListAdminUsersAsync(ctx.RequestAborted))
            .ToDictionary(u => u.UserId, u => u.Role);

        var users = new List<object>();
        await using var cmd = source.CreateCommand(
            "SELECT u.discord_id, u.username, u.avatar_url, u.user_id, " +
            "(SELECT COUNT(*) FROM el_mission m WHERE m.user_id = u.user_id) AS mission_count, " +
            "COALESCE((SELECT SUM(pg_column_size(m.*)) FROM el_mission m WHERE m.user_id = u.user_id), 0) + " +
            "COALESCE((SELECT SUM(pg_column_size(bk.*)) FROM el_backup bk WHERE bk.user_id = u.user_id), 0) + " +
            "COALESCE((SELECT SUM(pg_column_size(d.*)) FROM el_artifact_drops d WHERE d.user_id = u.user_id), 0) + " +
            "COALESCE((SELECT SUM(pg_column_size(s.*)) FROM el_settings s WHERE s.user_id = u.user_id), 0) + " +
            "COALESCE((SELECT SUM(pg_column_size(r.*)) FROM el_reports r WHERE r.user_id = u.user_id), 0) + " +
            "COALESCE((SELECT SUM(pg_column_size(g.*)) FROM el_report_groups g WHERE g.user_id = u.user_id), 0) + " +
            "COALESCE((SELECT SUM(pg_column_size(b.*)) FROM blobs b WHERE b.user_id = u.user_id), 0) AS storage_bytes, " +
            "(SELECT MAX(expires_at) FROM sessions se WHERE se.user_id = u.user_id) AS last_session " +
            "FROM users u ORDER BY u.username");
        await using var reader = await cmd.ExecuteReaderAsync(ctx.RequestAborted);
        while (await reader.ReadAsync(ctx.RequestAborted)) {
            var rowUserId = reader.GetGuid(3);
            users.Add(new {
                discordId = reader.IsDBNull(0) ? null : reader.GetString(0),
                username = reader.GetString(1),
                avatarUrl = reader.GetString(2),
                userId = rowUserId,
                missionCount = reader.GetInt64(4),
                storageBytes = reader.GetInt64(5),
                lastSession = reader.IsDBNull(6) ? (long?)null : reader.GetInt64(6),
                isAdmin = roleByUserId.GetValueOrDefault(rowUserId) == "admin",
            });
        }
        await JsonAsync(ctx, users);
    }

    // GET /api/v1/admin/metrics -> per-minute request totals + per-path tallies.
    public async Task Metrics(HttpContext ctx) {
        if (!await IsAdminAsync(ctx)) { await ForbidAsync(ctx); return; }
        await JsonAsync(ctx, new {
            minutes = metrics.SnapshotMinutes(),
            paths = metrics.SnapshotPaths(),
            spam = await spam.SnapshotAsync(),
        });
    }

    // DELETE /api/v1/admin/users/{userId}: remove a user (cascades sessions + blobs + el_*).
    public async Task DeleteUser(HttpContext ctx, string userId) {
        if (!await IsAdminAsync(ctx)) { await ForbidAsync(ctx); return; }
        if (!Guid.TryParse(userId, out var parsedUserId)) {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await JsonAsync(ctx, new { error = "invalid user id" });
            return;
        }
        if (string.Equals(userId, UserId(ctx), StringComparison.OrdinalIgnoreCase)) {
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
