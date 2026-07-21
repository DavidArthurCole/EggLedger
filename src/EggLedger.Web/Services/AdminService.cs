using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace EggLedger.Web.Services;



public sealed class AdminService(HttpClient http) {
    private const string Prefix = "api/v1/admin";

    public Task<IReadOnlyList<AdminUser>?> GetUsersAsync(string token, CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<AdminUser>>(token, "users", ct);

    public Task<AdminMetrics?> GetMetricsAsync(string token, CancellationToken ct = default) =>
        GetAsync<AdminMetrics>(token, "metrics", ct);

    public async Task<bool> DeleteUserAsync(string token, Guid userId, CancellationToken ct = default) {
        using var req = new HttpRequestMessage(HttpMethod.Delete, $"{Prefix}/users/{userId}");
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

public sealed record AdminUser(Guid UserId, string? DiscordId, string Username, string AvatarUrl, long MissionCount, long BackupCount, long ReportCount, long StorageBytes, long? LastSession, bool IsAdmin);

public sealed record AdminMetrics(IReadOnlyList<AdminMinute> Minutes, IReadOnlyList<AdminPath> Paths, IReadOnlyList<AdminSpam> Spam);

public sealed record AdminMinute(long MinuteEpochSeconds, int Total);

public sealed record AdminPath(string Path, long Count);

public sealed record AdminSpam(string Ip, string Method, string Path, string UserAgent, long FirstSeen, long LastSeen, long Hits);
