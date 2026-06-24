using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace EggLedger.Web.Services;

// Client for the sync-server admin API (/api/v1/admin/*). All calls are bearer-authed
// with the cloud session token; the server gates on the admin allowlist.
public sealed class AdminService(HttpClient http) {
    private const string Prefix = "api/v1/admin";

    public async Task<bool> IsAdminAsync(string token, CancellationToken ct = default) {
        try {
            var me = await GetAsync<AdminMe>(token, "me", ct).ConfigureAwait(false);
            return me?.IsAdmin ?? false;
        } catch {
            return false;
        }
    }

    public Task<IReadOnlyList<AdminUser>?> GetUsersAsync(string token, CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<AdminUser>>(token, "users", ct);

    public Task<AdminMetrics?> GetMetricsAsync(string token, CancellationToken ct = default) =>
        GetAsync<AdminMetrics>(token, "metrics", ct);

    public async Task<bool> DeleteUserAsync(string token, string discordId, CancellationToken ct = default) {
        using var req = new HttpRequestMessage(HttpMethod.Delete, $"{Prefix}/users/{Uri.EscapeDataString(discordId)}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resp = await http.SendAsync(req, ct).ConfigureAwait(false);
        return resp.IsSuccessStatusCode;
    }

    private async Task<T?> GetAsync<T>(string token, string path, CancellationToken ct) {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{Prefix}/{path}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resp = await http.SendAsync(req, ct).ConfigureAwait(false);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<T>(cancellationToken: ct).ConfigureAwait(false)
            : default;
    }
}

public sealed record AdminMe(bool IsAdmin);

public sealed record AdminUser(string DiscordId, string Username, string AvatarUrl, long BlobCount, long? LastSession, bool IsAdmin);

public sealed record AdminMetrics(IReadOnlyList<AdminMinute> Minutes, IReadOnlyList<AdminPath> Paths);

public sealed record AdminMinute(long MinuteEpochSeconds, int Total);

public sealed record AdminPath(string Path, long Count);
