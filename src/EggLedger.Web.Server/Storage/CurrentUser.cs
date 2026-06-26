using EggLedger.Web.Server.Auth;
using Microsoft.AspNetCore.Components.Authorization;

namespace EggLedger.Web.Server.Storage;

/// <summary>
/// Resolves the circuit's Discord user id from the framework AuthenticationState, read per op by
/// <see cref="PostgresIndexedDb"/> to scope storage to the right tenant.
/// </summary>
/// <remarks>
/// <see cref="GetDiscordIdAsync"/> is tolerant (null when unauthenticated, for reads);
/// <see cref="RequireAsync"/> throws (for writes).
/// </remarks>
public sealed class CurrentUser(AuthenticationStateProvider auth) {
    public async Task<string?> GetDiscordIdAsync() {
        var state = await auth.GetAuthenticationStateAsync().ConfigureAwait(false);
        var id = state.User.FindFirst(AuthScheme.DiscordIdClaim)?.Value;
        return string.IsNullOrEmpty(id) ? null : id;
    }

    public async Task<bool> IsAuthenticatedAsync() => await GetDiscordIdAsync().ConfigureAwait(false) is not null;

    public async Task<string> RequireAsync() =>
        await GetDiscordIdAsync().ConfigureAwait(false)
        ?? throw new InvalidOperationException(
            "storage accessed without an authenticated Discord principal; the data UI must be gated behind authentication");
}
