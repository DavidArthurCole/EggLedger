using EggLedger.Web.Server.Auth;
using Microsoft.AspNetCore.Components.Authorization;

namespace EggLedger.Web.Server.Storage;

public sealed class CurrentUser(AuthenticationStateProvider auth) {
    public async Task<Guid?> GetUserIdAsync() {
        var state = await auth.GetAuthenticationStateAsync().ConfigureAwait(false);
        var id = state.User.FindFirst(AuthScheme.UserIdClaim)?.Value;
        return Guid.TryParse(id, out var parsed) ? parsed : null;
    }

    public async Task<bool> IsAuthenticatedAsync() => await GetUserIdAsync().ConfigureAwait(false) is not null;

    public async Task<Guid> RequireUserIdAsync() =>
        await GetUserIdAsync().ConfigureAwait(false)
        ?? throw new InvalidOperationException(
            "your session has expired. Please refresh the page and log in with Discord again");
}
