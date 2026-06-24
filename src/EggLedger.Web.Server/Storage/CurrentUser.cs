using EggLedger.Web.Server.Auth;
using Microsoft.AspNetCore.Components.Authorization;

namespace EggLedger.Web.Server.Storage;

/// <summary>
/// Resolves the authenticated Discord user id for the current circuit from the framework
/// AuthenticationState (cookie-auth principal). Read by <see cref="PostgresIndexedDb"/> on
/// each op so storage is scoped to the right tenant. <see cref="GetDiscordIdAsync"/> is the
/// tolerant accessor (null when unauthenticated, used by reads); <see cref="RequireAsync"/>
/// throws (used by writes). The data UI is gated behind an authenticated principal.
/// </summary>
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
