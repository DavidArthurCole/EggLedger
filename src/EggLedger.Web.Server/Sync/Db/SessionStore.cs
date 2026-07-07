using Npgsql;
using SyncKit.Auth;

namespace EggLedger.Web.Server.Sync.Db;

public sealed class SessionStore(NpgsqlDataSource source) : ISessionStore {
    // The tuple's "DiscordId" name is SyncKit.Auth's, predating this migration to
    // provider-neutral user_id; SyncKit can't be released mid-migration to rename it.
    // RequireAuth only stamps this string into X-Discord-ID and never inspects its contents,
    // so it's safe to carry the user_id GUID string here instead.
    public async Task<(bool Found, string DiscordId, long ExpiresAt)> LookupAsync(string token, CancellationToken ct) {
        await using var cmd = source.CreateCommand(
            "SELECT user_id, expires_at FROM sessions WHERE token = $1");
        cmd.Parameters.AddWithValue(token);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return (false, string.Empty, 0);
        return (true, reader.GetGuid(0).ToString(), reader.GetInt64(1));
    }

    public async Task TouchAsync(string token, long newExpiresAt, CancellationToken ct) {
        await using var cmd = source.CreateCommand(
            "UPDATE sessions SET expires_at = $1 WHERE token = $2");
        cmd.Parameters.AddWithValue(newExpiresAt);
        cmd.Parameters.AddWithValue(token);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
