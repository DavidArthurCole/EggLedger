using EggLedger.Domain.Api;
using EggLedger.Domain.Ei;
using EggLedger.Domain.MissionQuery;
using EggLedger.Web.Data;

namespace EggLedger.Web.Services;

/// <summary>Adds an account from an EID: validate the first-contact backup, then persist. Mission fetch is a separate step driven by <see cref="FetchService"/>.</summary>
public sealed class AddAccountService {
    private static readonly TimeSpan FirstContactTimeout = TimeSpan.FromSeconds(20);

    private readonly ApiClient _api;
    private readonly IndexedDbAccountStore _accounts;
    private readonly IApiPayloadDecoder _decoder;

    public AddAccountService(ApiClient api, IndexedDbAccountStore accounts, IApiPayloadDecoder decoder) {
        _api = api;
        _accounts = accounts;
        _decoder = decoder;
    }

    /// <summary>Fetches the backup, builds and stores the account. Throws on an invalid id or a network/decode failure.</summary>
    public async Task<AccountInfo> AddAccountAsync(string eid, CancellationToken cancellationToken = default) {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(FirstContactTimeout);

        byte[] payload = await _api.RequestFirstContactRawPayloadAsync(eid, cts.Token).ConfigureAwait(false);
        var fc = await _decoder.DecodeFirstContactAsync(payload, cts.Token).ConfigureAwait(false);
        var invalid = fc.Validate();
        if (invalid is not null) {
            throw new InvalidOperationException(
                $"please double check your ID: error fetching backup for player {eid}: {invalid.Message}", invalid);
        }

        var account = AccountFactory.FromBackup(eid, fc.Backup!);
        await _accounts.AddKnownAccountAsync(account).ConfigureAwait(false);
        return account;
    }
}
