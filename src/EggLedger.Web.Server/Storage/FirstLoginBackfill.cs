using System.Text.Json;
using EggLedger.Web.Data;
using EggLedger.Web.Services;
using EggLedger.Web.Settings;
using Npgsql;

namespace EggLedger.Web.Server.Storage;

/// <summary>
/// On a user's first login to the unified host, restores their settings + reports from the
/// cloud blobs the old sync server already holds (encrypted in the blobs table). The new
/// per-user Postgres store starts empty, so without this returning users would lose their
/// synced settings/reports. Mission history is NOT in any blob (only the first-contact
/// backup is), so it is not backfilled - the user re-fetches once on the new host.
///
/// Idempotent + cheap: runs only when the user's settings store is empty (the fresh-store
/// signal). Reads ciphertext + the user's encryption key from Postgres and decrypts in
/// process via IBlobCipher (managed AES-GCM); no HTTP, no Bearer token.
/// </summary>
public sealed class FirstLoginBackfill(
    NpgsqlDataSource source,
    CurrentUser user,
    IBlobCipher cipher,
    IndexedDbSettings settings,
    IndexedDbReportStore reports) {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task RunIfNeededAsync(CancellationToken ct = default) {
        var discordId = await user.GetDiscordIdAsync().ConfigureAwait(false);
        if (discordId is null) {
            return;
        }

        // Fresh store signal: no settings rows yet for this user.
        var existing = await settings.GetAllSettingsAsync().ConfigureAwait(false);
        if (existing.Count > 0) {
            return;
        }

        var encKey = await EncryptionKeyAsync(discordId, ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(encKey)) {
            return;
        }

        await RestoreSettingsAsync(discordId, encKey, ct).ConfigureAwait(false);
        await RestoreReportsAsync(discordId, encKey, ct).ConfigureAwait(false);
    }

    private async Task RestoreSettingsAsync(string discordId, string encKey, CancellationToken ct) {
        var blob = await DecryptBlobAsync<CloudSyncableSettings>(discordId, encKey, CloudSyncBlobs.SettingsBlob, ct)
            .ConfigureAwait(false);
        if (blob is not null) {
            await settings.SetSettingsAsync(CloudSyncBlobs.UnpackSettings(blob)).ConfigureAwait(false);
        }
    }

    private async Task RestoreReportsAsync(string discordId, string encKey, CancellationToken ct) {
        var blob = await DecryptBlobAsync<CloudReportsBlob>(discordId, encKey, CloudSyncBlobs.ReportsBlob, ct)
            .ConfigureAwait(false);
        if (blob is null) {
            return;
        }
        var existingGroups = (await reports.RetrieveAccountGroupsAsync("").ConfigureAwait(false)).Select(g => g.Id).ToList();
        var existingReports = new List<string>();
        var (groups, rows) = CloudSyncBlobs.SelectReportsToImport(blob, existingGroups, existingReports);
        foreach (var g in groups) {
            await reports.InsertReportGroupAsync(g).ConfigureAwait(false);
        }
        foreach (var r in rows) {
            await reports.InsertReportAsync(r).ConfigureAwait(false);
        }
    }

    private async Task<string?> EncryptionKeyAsync(string discordId, CancellationToken ct) {
        await using var cmd = source.CreateCommand("SELECT encryption_key FROM users WHERE discord_id = $1");
        cmd.Parameters.AddWithValue(discordId);
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result as string;
    }

    private async Task<T?> DecryptBlobAsync<T>(string discordId, string encKey, string name, CancellationToken ct) {
        string? ciphertext = null;
        await using (var cmd = source.CreateCommand("SELECT ciphertext FROM blobs WHERE discord_id = $1 AND name = $2")) {
            cmd.Parameters.AddWithValue(discordId);
            cmd.Parameters.AddWithValue(name);
            var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            ciphertext = result as string;
        }
        if (string.IsNullOrEmpty(ciphertext)) {
            return default;
        }
        try {
            var plaintext = await cipher.DecryptAsync(encKey, ciphertext, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<T>(plaintext, Json);
        } catch {
            // Corrupt/incompatible blob: skip rather than block login. The manual restore
            // in the Settings tab remains available.
            return default;
        }
    }
}
