using System.Text.Json;
using EggLedger.Web.Components.Admin;
using EggLedger.Web.Server.Sync.Auth;
using SyncKit.Contract;

namespace EggLedger.Web.Server.Sync.Admin;

public sealed class AdminEndpoints(IAdminData data, ICurrentUser currentUser) {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
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
        await JsonAsync(ctx, await data.GetUsersAsync(ctx.RequestAborted));
    }

    public async Task DeleteUser(HttpContext ctx, string userId) {
        if (!await IsAdminAsync(ctx)) { await ForbidAsync(ctx); return; }
        if (!Guid.TryParse(userId, out var parsedUserId)) {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await JsonAsync(ctx, new { error = "invalid user id" });
            return;
        }
        if (parsedUserId == currentUser.UserId(ctx)) {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await JsonAsync(ctx, new { error = "cannot delete your own account" });
            return;
        }
        var deleted = await data.DeleteUserAsync(parsedUserId, ctx.RequestAborted);
        await JsonAsync(ctx, new { deleted });
    }
}
