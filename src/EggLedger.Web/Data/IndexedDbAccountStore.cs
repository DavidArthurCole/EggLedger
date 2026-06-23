using System.Text.Json;
using System.Text.Json.Serialization;
using EggLedger.Domain.MissionQuery;

namespace EggLedger.Web.Data;

/// <summary>
/// Known-account persistence. C# port of the Go storage.AppStorage
/// known-accounts + active-account fields, which the desktop app keeps in its
/// settings table. The browser has no dedicated accounts object store, so the
/// account list is stored as one JSON blob under the <c>known_accounts</c>
/// settings key and the selection under <c>active_account_id</c>, reusing the
/// existing <c>settings</c> store (no IndexedDB schema bump). Upsert by id keeps
/// at most one row per account, matching Go's AddKnownAccount.
/// </summary>
public sealed class IndexedDbAccountStore
{
    internal const string KnownAccountsKey = "known_accounts";
    internal const string ActiveAccountKey = "active_account_id";

    private readonly IndexedDbSettings _settings;

    public IndexedDbAccountStore(IndexedDbSettings settings)
    {
        _settings = settings;
    }

    /// <summary>All known accounts, in insertion order. Empty when none stored.</summary>
    public async Task<List<AccountInfo>> GetKnownAccountsAsync()
    {
        var all = await _settings.GetAllSettingsAsync();
        return Deserialize(all);
    }

    /// <summary>
    /// Upserts an account by id (replacing an existing entry, else appending),
    /// then persists the list. Mirrors Go AddKnownAccount.
    /// </summary>
    public async Task AddKnownAccountAsync(AccountInfo account)
    {
        var all = await _settings.GetAllSettingsAsync();
        var list = Deserialize(all);
        int i = list.FindIndex(a => a.Id == account.Id);
        if (i >= 0)
        {
            list[i] = account;
        }
        else
        {
            list.Add(account);
        }
        await _settings.SetSettingAsync(KnownAccountsKey, Serialize(list));
    }

    /// <summary>Removes the account with the given id, if present.</summary>
    public async Task RemoveKnownAccountAsync(string id)
    {
        var all = await _settings.GetAllSettingsAsync();
        var list = Deserialize(all);
        int removed = list.RemoveAll(a => a.Id == id);
        if (removed > 0)
        {
            await _settings.SetSettingAsync(KnownAccountsKey, Serialize(list));
        }
    }

    /// <summary>Persisted active account id, or null when unset/blank.</summary>
    public async Task<string?> GetActiveAccountIdAsync()
    {
        var all = await _settings.GetAllSettingsAsync();
        if (all.TryGetValue(ActiveAccountKey, out var id) && !string.IsNullOrEmpty(id))
        {
            return id;
        }
        return null;
    }

    /// <summary>Persists the active account id (empty string clears it).</summary>
    public async Task SetActiveAccountIdAsync(string id) =>
        await _settings.SetSettingAsync(ActiveAccountKey, id ?? "");

    private static List<AccountInfo> Deserialize(IReadOnlyDictionary<string, string> settings)
    {
        if (!settings.TryGetValue(KnownAccountsKey, out var raw) || string.IsNullOrEmpty(raw))
        {
            return new List<AccountInfo>();
        }
        try
        {
            var rows = JsonSerializer.Deserialize<List<AccountInfoRow>>(raw, Rows.JsonOptions);
            return rows is null
                ? new List<AccountInfo>()
                : rows.ConvertAll(AccountInfoRow.ToAccount);
        }
        catch (JsonException)
        {
            // Corrupt blob: start clean rather than wedge the Ledger tab.
            return new List<AccountInfo>();
        }
    }

    private static string Serialize(List<AccountInfo> list)
    {
        var rows = list.ConvertAll(AccountInfoRow.FromAccount);
        return JsonSerializer.Serialize(rows, Rows.JsonOptions);
    }
}

/// <summary>
/// Persistence DTO for a known account. The explicit snake_case
/// <see cref="JsonPropertyNameAttribute"/> names decouple the stored blob from
/// the Domain <see cref="AccountInfo"/> property names, so renaming a Domain
/// field cannot silently break round-trip of already-stored blobs.
/// </summary>
public sealed record AccountInfoRow
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("nickname")]
    public string Nickname { get; init; } = "";

    [JsonPropertyName("eb_string")]
    public string EBString { get; init; } = "";

    [JsonPropertyName("account_color")]
    public string AccountColor { get; init; } = "";

    [JsonPropertyName("se_string")]
    public string SeString { get; init; } = "";

    [JsonPropertyName("pe_count")]
    public int PeCount { get; init; }

    [JsonPropertyName("te_count")]
    public int TeCount { get; init; }

    public static AccountInfoRow FromAccount(AccountInfo a) => new()
    {
        Id = a.Id,
        Nickname = a.Nickname,
        EBString = a.EBString,
        AccountColor = a.AccountColor,
        SeString = a.SeString,
        PeCount = a.PeCount,
        TeCount = a.TeCount,
    };

    public static AccountInfo ToAccount(AccountInfoRow r) => new()
    {
        Id = r.Id,
        Nickname = r.Nickname,
        EBString = r.EBString,
        AccountColor = r.AccountColor,
        SeString = r.SeString,
        PeCount = r.PeCount,
        TeCount = r.TeCount,
    };
}
