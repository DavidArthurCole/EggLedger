using Npgsql;

namespace EggLedger.Web.Server.Auth;

// Resolves a user_id for an Authentik OIDC login: exact-match an existing authentik identity,
// else auto-link via a matching discord identity (Authentik's Discord source surfaces the
// federated Discord snowflake as the discord_id claim), else create a brand-new account.
public sealed class AuthentikIdentityResolver(NpgsqlDataSource source) {
    public async Task<Guid> ResolveAsync(string sub, string? discordId, CancellationToken ct) {
        await using var conn = await source.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var existing = await LookupAsync(conn, tx, "authentik", sub, ct);
        if (existing is { } found) {
            await tx.CommitAsync(ct);
            return found;
        }

        Guid userId;
        if (!string.IsNullOrEmpty(discordId) && await LookupAsync(conn, tx, "discord", discordId, ct) is { } linked) {
            userId = linked;
        } else {
            await using var insertUser = new NpgsqlCommand(
                "INSERT INTO users (discord_id, created_at) VALUES ($1, $2) RETURNING user_id", conn, tx);
            insertUser.Parameters.AddWithValue((object?)discordId ?? DBNull.Value);
            insertUser.Parameters.AddWithValue(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            userId = (Guid)(await insertUser.ExecuteScalarAsync(ct))!;
        }

        await using var insertIdentity = new NpgsqlCommand(
            "INSERT INTO identities (user_id, provider, subject) VALUES ($1, 'authentik', $2) ON CONFLICT (provider, subject) DO NOTHING",
            conn, tx);
        insertIdentity.Parameters.AddWithValue(userId);
        insertIdentity.Parameters.AddWithValue(sub);
        var affected = await insertIdentity.ExecuteNonQueryAsync(ct);

        // A concurrent identical first-login can win the (provider, subject) insert first,
        // making this one a no-op; re-select and return the winner's user_id instead of the
        // caller's freshly-built one, or the loser's userId is orphaned with no identity row.
        if (affected == 0) {
            var winner = await LookupAsync(conn, tx, "authentik", sub, ct);
            await tx.CommitAsync(ct);
            return winner ?? userId;
        }

        await tx.CommitAsync(ct);
        return userId;
    }

    private static async Task<Guid?> LookupAsync(NpgsqlConnection conn, NpgsqlTransaction tx, string provider, string subject, CancellationToken ct) {
        await using var cmd = new NpgsqlCommand("SELECT user_id FROM identities WHERE provider = $1 AND subject = $2", conn, tx);
        cmd.Parameters.AddWithValue(provider);
        cmd.Parameters.AddWithValue(subject);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is Guid g ? g : null;
    }
}
