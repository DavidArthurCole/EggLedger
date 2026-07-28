using System.Security.Claims;

namespace EggLedger.Web.Components.Auth;

public interface IPairCompletion {
    Task<bool> CompleteAsync(ClaimsPrincipal user, string state, CancellationToken ct);
}
