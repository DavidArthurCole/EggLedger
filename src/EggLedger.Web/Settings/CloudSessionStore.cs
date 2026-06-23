using System.Globalization;
using EggLedger.Web.Data;
using EggLedger.Web.Services;

namespace EggLedger.Web.Settings;

/// <summary>
/// Persists the connected cloud-sync session and its sync timestamps in
/// <see cref="IndexedDbSettings"/>, using the same setting keys the Go app uses
/// (storage.go cloud_* keys). C# stand-in for the Go AppStorage cloud fields: the
/// browser has no SQLite settings table, so the token / encryption key / Discord
/// identity / last-push / last-pull / auto-sync flag live as string settings. The
/// <see cref="CloudSyncService"/> stays stateless; this owns the persisted
/// <see cref="CloudSession"/>.
/// </summary>
public sealed class CloudSessionStore
{
    public const string KeyToken = "cloud_session_token";
    public const string KeyEncryptionKey = "cloud_encryption_key";
    public const string KeyUsername = "cloud_discord_username";
    public const string KeyAvatarUrl = "cloud_discord_avatar_url";
    public const string KeyLastPushAt = "cloud_last_push_at";
    public const string KeyLastPullAt = "cloud_last_pull_at";
    public const string KeyAutoSync = "cloud_auto_sync";

    /// <summary>
    /// Browser-only: the poll state token saved before the full-page redirect to
    /// Discord, so the reloaded SPA can resume polling. The desktop app has no
    /// equivalent (it polls in-process without navigating away).
    /// </summary>
    public const string KeyPendingAuthState = "cloud_pending_auth_state";

    private readonly IndexedDbSettings _settings;

    public CloudSessionStore(IndexedDbSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// The persisted session, or null when no token is stored (disconnected).
    /// </summary>
    public async Task<CloudSession?> GetSessionAsync()
    {
        var all = await _settings.GetAllSettingsAsync();
        if (!all.TryGetValue(KeyToken, out var token) || string.IsNullOrEmpty(token))
        {
            return null;
        }
        return new CloudSession(
            token,
            all.GetValueOrDefault(KeyUsername, ""),
            all.GetValueOrDefault(KeyAvatarUrl, ""),
            all.GetValueOrDefault(KeyEncryptionKey, ""));
    }

    /// <summary>Persists a freshly authed session (token + identity + key).</summary>
    public async Task SaveSessionAsync(CloudSession session)
    {
        await _settings.SetSettingsAsync(new Dictionary<string, string>
        {
            [KeyToken] = session.Token,
            [KeyEncryptionKey] = session.EncryptionKey,
            [KeyUsername] = session.Username,
            [KeyAvatarUrl] = session.AvatarUrl,
        });
    }

    /// <summary>Clears the session creds (disconnect). Timestamps are left as-is.</summary>
    public async Task ClearSessionAsync()
    {
        await _settings.SetSettingsAsync(new Dictionary<string, string>
        {
            [KeyToken] = "",
            [KeyEncryptionKey] = "",
            [KeyUsername] = "",
            [KeyAvatarUrl] = "",
        });
    }

    /// <summary>Last successful push time (Unix seconds), or 0 when never pushed.</summary>
    public async Task<long> GetLastPushAtAsync() => await GetUnixAsync(KeyLastPushAt);

    /// <summary>Last successful pull time (Unix seconds), or 0 when never pulled.</summary>
    public async Task<long> GetLastPullAtAsync() => await GetUnixAsync(KeyLastPullAt);

    public async Task SetLastPushAtAsync(long unixSeconds) =>
        await _settings.SetSettingAsync(KeyLastPushAt, unixSeconds.ToString(CultureInfo.InvariantCulture));

    public async Task SetLastPullAtAsync(long unixSeconds) =>
        await _settings.SetSettingAsync(KeyLastPullAt, unixSeconds.ToString(CultureInfo.InvariantCulture));

    /// <summary>Whether settings changes auto-trigger a sync. Default false.</summary>
    public async Task<bool> GetAutoSyncAsync()
    {
        var all = await _settings.GetAllSettingsAsync();
        return all.TryGetValue(KeyAutoSync, out var raw) && bool.TryParse(raw, out var v) && v;
    }

    public async Task SetAutoSyncAsync(bool enabled) =>
        await _settings.SetSettingAsync(KeyAutoSync, SettingsModel.FormatBool(enabled));

    /// <summary>The pending auth state to resume polling, or null when none is set.</summary>
    public async Task<string?> GetPendingAuthStateAsync()
    {
        var all = await _settings.GetAllSettingsAsync();
        return all.TryGetValue(KeyPendingAuthState, out var s) && !string.IsNullOrEmpty(s) ? s : null;
    }

    public async Task SetPendingAuthStateAsync(string state) =>
        await _settings.SetSettingAsync(KeyPendingAuthState, state);

    public async Task ClearPendingAuthStateAsync() =>
        await _settings.SetSettingAsync(KeyPendingAuthState, "");

    private async Task<long> GetUnixAsync(string key)
    {
        var all = await _settings.GetAllSettingsAsync();
        return all.TryGetValue(key, out var raw)
            && long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v
            : 0;
    }
}
