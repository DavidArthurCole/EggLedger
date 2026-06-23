using EggLedger.Domain.Api;
using EggLedger.Domain.Ei;
using EggLedger.Domain.MissionQuery;
using EggLedger.Web.Data;

namespace EggLedger.Web.Services;

/// <summary>
/// Adds an account from an EID. C# port of the Go <c>addAccount</c> binding:
/// fetch + validate the first-contact backup, shape the display fields via
/// <see cref="AccountFactory"/>, then persist the account. The subsequent
/// mission fetch is driven separately by <see cref="FetchService"/> from the UI,
/// matching the desktop flow where adding an account and fetching its missions
/// are distinct steps.
/// </summary>
public sealed class AddAccountService
{
    private static readonly TimeSpan FirstContactTimeout = TimeSpan.FromSeconds(20);

    private readonly ApiClient _api;
    private readonly IndexedDbAccountStore _accounts;

    public AddAccountService(ApiClient api, IndexedDbAccountStore accounts)
    {
        _api = api;
        _accounts = accounts;
    }

    /// <summary>
    /// Fetches the backup for <paramref name="eid"/>, builds the account, and
    /// stores it. Throws on an invalid id (Go's "please double check your ID"
    /// wrap) or a network/decode failure. Returns the stored account.
    /// </summary>
    public async Task<AccountInfo> AddAccountAsync(string eid, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(FirstContactTimeout);

        byte[] payload = await _api.RequestFirstContactRawPayloadAsync(eid, cts.Token).ConfigureAwait(false);
        var fc = _api.DecodeFirstContactPayload(payload);
        var invalid = fc.Validate();
        if (invalid is not null)
        {
            throw new InvalidOperationException(
                $"please double check your ID: error fetching backup for player {eid}: {invalid.Message}", invalid);
        }

        var account = AccountFactory.FromBackup(eid, fc.Backup!);
        await _accounts.AddKnownAccountAsync(account).ConfigureAwait(false);
        return account;
    }
}
