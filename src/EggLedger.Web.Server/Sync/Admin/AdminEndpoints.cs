using System.Text.Json;
using EggLedger.Web.Server.Sync.Auth;
using Npgsql;
using SyncKit.Identity.Client;

namespace EggLedger.Web.Server.Sync.Admin;

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

    public async Task Me(HttpContext ctx) =>
     await JsonAsync(ctx, new { isAdmin = await IsAdminAsync(ctx) });

    public async Task Users(HttpContext ctx) {
        if (!await IsAdminAsync(ctx)) { await ForbidAsync(ctx); return; }

        var roleByUserId = (await identity.ListAdminUsersAsync(ctx.RequestAborted))
.ToDictionary(u => u.UserId, u => u.Role);

        var users = new List<object>();
        await using var cmd = source.CreateCommand(
            "WITH mission_agg AS (" +
            "  SELECT user_id, COUNT(*) AS cnt, SUM(pg_column_size(m.*)) AS bytes FROM el_mission m GROUP BY user_id" +
            "), backup_agg AS (" +
            "  SELECT user_id, COUNT(*) AS cnt, SUM(pg_column_size(bk.*)) AS bytes FROM el_backup bk GROUP BY user_id" +
            "), drops_agg AS (" +
            "  SELECT user_id, SUM(pg_column_size(d.*)) AS bytes FROM el_artifact_drops d GROUP BY user_id" +
            "), settings_agg AS (" +
            "  SELECT user_id, SUM(pg_column_size(s.*)) AS bytes FROM el_settings s GROUP BY user_id" +
            "), reports_agg AS (" +
            "  SELECT user_id, COUNT(*) AS cnt, SUM(pg_column_size(r.*)) AS bytes FROM el_reports r GROUP BY user_id" +
            "), groups_agg AS (" +
            "  SELECT user_id, SUM(pg_column_size(g.*)) AS bytes FROM el_report_groups g GROUP BY user_id" +
            "), blobs_agg AS (" +
            "  SELECT user_id, SUM(pg_column_size(b.*)) AS bytes FROM blobs b GROUP BY user_id" +
            "), session_agg AS (" +
            "  SELECT user_id, MAX(expires_at) AS last_session FROM sessions GROUP BY user_id" +
            ") " +
            "SELECT u.discord_id, u.username, u.avatar_url, u.user_id, " +
            "COALESCE(mission_agg.cnt, 0), COALESCE(backup_agg.cnt, 0), COALESCE(reports_agg.cnt, 0), " +
            "COALESCE(mission_agg.bytes, 0) + COALESCE(backup_agg.bytes, 0) + COALESCE(drops_agg.bytes, 0) + " +
            "COALESCE(settings_agg.bytes, 0) + COALESCE(reports_agg.bytes, 0) + COALESCE(groups_agg.bytes, 0) + " +
            "COALESCE(blobs_agg.bytes, 0), " +
            "session_agg.last_session " +
            "FROM users u " +
            "LEFT JOIN mission_agg ON mission_agg.user_id = u.user_id " +
            "LEFT JOIN backup_agg ON backup_agg.user_id = u.user_id " +
            "LEFT JOIN drops_agg ON drops_agg.user_id = u.user_id " +
            "LEFT JOIN settings_agg ON settings_agg.user_id = u.user_id " +
            "LEFT JOIN reports_agg ON reports_agg.user_id = u.user_id " +
            "LEFT JOIN groups_agg ON groups_agg.user_id = u.user_id " +
            "LEFT JOIN blobs_agg ON blobs_agg.user_id = u.user_id " +
            "LEFT JOIN session_agg ON session_agg.user_id = u.user_id " +
            "ORDER BY u.username");
        await using var reader = await cmd.ExecuteReaderAsync(ctx.RequestAborted);
        while (await reader.ReadAsync(ctx.RequestAborted)) {
            var rowUserId = reader.GetGuid(3);
            users.Add(new {
                discordId = reader.IsDBNull(0) ? null : reader.GetString(0),
                username = reader.GetString(1),
                avatarUrl = reader.GetString(2),
                userId = rowUserId,
                missionCount = reader.GetInt64(4),
                backupCount = reader.GetInt64(5),
                reportCount = reader.GetInt64(6),
                storageBytes = reader.GetInt64(7),
                lastSession = reader.IsDBNull(8) ? (long?)null : reader.GetInt64(8),
                isAdmin = roleByUserId.GetValueOrDefault(rowUserId) == "admin",
            });
        }
        await JsonAsync(ctx, users);
    }

    public async Task Metrics(HttpContext ctx) {
        if (!await IsAdminAsync(ctx)) { await ForbidAsync(ctx); return; }
        await JsonAsync(ctx, new {
            minutes = metrics.SnapshotMinutes(),
            paths = metrics.SnapshotPaths(),
            spam = await spam.SnapshotAsync(),
        });
    }

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
