using System.Text.Json;
using EggLedger.Domain.MissionQuery;
using EggLedger.Web.Data;
using EggLedger.Web.Services;
using EggLedger.Web.Settings;
using Npgsql;

namespace EggLedger.Web.Server.Storage;

/// <summary>
/// First login to the unified host: restores accounts + settings + reports from the old sync
/// server's encrypted blobs so returning users don't lose synced data on the empty Postgres store.
/// </summary>
/// <remarks>
/// Mission history is not in any blob (only the first-contact backup is), so the user re-fetches
/// once. Guard is "no known accounts yet"; settings-empty is unreliable since the app writes
/// settings early. Decrypts in process via IBlobCipher (managed AES-GCM); no HTTP, no Bearer.
/// </remarks>
public sealed class FirstLoginBackfill(
    NpgsqlDataSource source,
    CurrentUser user,
    IBlobCipher cipher,
    IndexedDbAccountStore accounts,
    IndexedDbSettings settings,
    IndexedDbReportStore reports) {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task RunIfNeededAsync(CancellationToken ct = default) {
        var discordId = await user.GetDiscordIdAsync().ConfigureAwait(false);
        if (discordId is null) {
            return;
        }

        // Fresh-store signal: no known accounts yet. (Settings are written early by the app,
        // so settings-empty is unreliable; accounts only exist after restore or a fetch.)
        var known = await accounts.GetKnownAccountsAsync().ConfigureAwait(false);
        if (known.Count > 0) {
            return;
        }

        var encKey = await EncryptionKeyAsync(discordId, ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(encKey)) {
            return;
        }

        await RestoreAccountsAsync(discordId, encKey, ct).ConfigureAwait(false);
        await RestoreSettingsAsync(discordId, encKey, ct).ConfigureAwait(false);
        await RestoreReportsAsync(discordId, encKey, ct).ConfigureAwait(false);
    }

    private async Task RestoreAccountsAsync(string discordId, string encKey, CancellationToken ct) {
        var list = await DecryptBlobAsync<List<AccountInfo>>(discordId, encKey, CloudSyncBlobs.AccountsBlob, ct)
            .ConfigureAwait(false);
        if (list is null) {
            return;
        }
        foreach (var acct in list) {
            await accounts.AddKnownAccountAsync(acct).ConfigureAwait(false);
        }
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
