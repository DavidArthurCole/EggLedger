using System.Globalization;
using EggLedger.Web.Data;
using EggLedger.Web.Services;

namespace EggLedger.Web.Settings;

public sealed class CloudSessionStore {
    public const string KeyToken = "cloud_session_token";
    public const string KeyEncryptionKey = "cloud_encryption_key";
    public const string KeyUsername = "cloud_discord_username";
    public const string KeyAvatarUrl = "cloud_discord_avatar_url";
    public const string KeyLastPushAt = "cloud_last_push_at";
    public const string KeyLastPullAt = "cloud_last_pull_at";
    public const string KeyAutoSync = "cloud_auto_sync";

    public const string KeyPendingAuthState = "cloud_pending_auth_state";
    private readonly IndexedDbSettings _settings;

    public CloudSessionStore(IndexedDbSettings settings) {
        _settings = settings;
    }

    public async Task<CloudSession?> GetSessionAsync() {
        var all = await _settings.GetAllSettingsAsync();
        if (!all.TryGetValue(KeyToken, out var token) || string.IsNullOrEmpty(token)) {
            return null;
        }
        return new CloudSession(
            token,
            all.GetValueOrDefault(KeyUsername, ""),
            all.GetValueOrDefault(KeyAvatarUrl, ""),
            all.GetValueOrDefault(KeyEncryptionKey, ""));
    }

    public async Task SaveSessionAsync(CloudSession session) {
        await _settings.SetSettingsAsync(new Dictionary<string, string> {
            [KeyToken] = session.Token,
            [KeyEncryptionKey] = session.EncryptionKey,
            [KeyUsername] = session.Username,
            [KeyAvatarUrl] = session.AvatarUrl,
        });
    }

    public async Task ClearSessionAsync() {
        await _settings.SetSettingsAsync(new Dictionary<string, string> {
            [KeyToken] = "",
            [KeyEncryptionKey] = "",
            [KeyUsername] = "",
            [KeyAvatarUrl] = "",
        });
    }

    public async Task<long> GetLastPushAtAsync() => await GetUnixAsync(KeyLastPushAt);

    public async Task<long> GetLastPullAtAsync() => await GetUnixAsync(KeyLastPullAt);

    public async Task SetLastPushAtAsync(long unixSeconds) =>
        await _settings.SetSettingAsync(KeyLastPushAt, unixSeconds.ToString(CultureInfo.InvariantCulture));

    public async Task SetLastPullAtAsync(long unixSeconds) =>
        await _settings.SetSettingAsync(KeyLastPullAt, unixSeconds.ToString(CultureInfo.InvariantCulture));

    public async Task<bool> GetAutoSyncAsync() {
        var all = await _settings.GetAllSettingsAsync();
        return all.TryGetValue(KeyAutoSync, out var raw) && bool.TryParse(raw, out var v) && v;
    }

    public async Task SetAutoSyncAsync(bool enabled) =>
        await _settings.SetSettingAsync(KeyAutoSync, SettingsModel.FormatBool(enabled));

    public async Task<string?> GetPendingAuthStateAsync() {
        var all = await _settings.GetAllSettingsAsync();
        return all.TryGetValue(KeyPendingAuthState, out var s) && !string.IsNullOrEmpty(s) ? s : null;
    }

    public async Task SetPendingAuthStateAsync(string state) =>
        await _settings.SetSettingAsync(KeyPendingAuthState, state);

    public async Task ClearPendingAuthStateAsync() =>
        await _settings.SetSettingAsync(KeyPendingAuthState, "");

    private async Task<long> GetUnixAsync(string key) {
        var all = await _settings.GetAllSettingsAsync();
        return all.TryGetValue(key, out var raw)
            && long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v
            : 0;
    }
}
