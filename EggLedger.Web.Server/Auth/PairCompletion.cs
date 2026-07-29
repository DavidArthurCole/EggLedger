using System.Security.Claims;
using EggLedger.Web.Components.Auth;
using EggLedger.Web.Server.Sync.Auth;

namespace EggLedger.Web.Server.Auth;

public sealed class PairCompletion(AuthEndpoints auth) : IPairCompletion {
    public Task<bool> CompleteAsync(ClaimsPrincipal user, string state, CancellationToken ct) =>
        auth.CompletePairingAsync(user, state, ct);
}
