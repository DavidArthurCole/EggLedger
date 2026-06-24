using EggLedger.Web.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Photino.NET;

namespace EggLedger.Desktop.Platform;

/// <summary>
/// Registers the native desktop <see cref="IPlatformCapabilities"/>, replacing the
/// browser no-op stub. Call AFTER AddEggLedgerWeb so this override wins.
/// </summary>
public static class DesktopPlatformRegistration {
    /// <summary>
    /// Swaps in the native impl over <paramref name="window"/>, registering the
    /// process runner and window wrapper as singletons.
    /// </summary>
    public static IServiceCollection AddDesktopPlatformCapabilities(
        this IServiceCollection services, PhotinoWindow window)
        => services.AddDesktopPlatformCapabilities(new ProcessRunner(), new PhotinoDesktopWindow(window));

    /// <summary>Registers the native capabilities over explicit seams (used by tests with fakes).</summary>
    public static IServiceCollection AddDesktopPlatformCapabilities(
        this IServiceCollection services, IProcessRunner processRunner, IDesktopWindow window) {
        services.AddSingleton(processRunner);
        services.AddSingleton(window);

        // Drop the browser no-op stub so the native impl is resolved everywhere.
        services.RemoveAll<IPlatformCapabilities>();
        services.AddSingleton<IPlatformCapabilities>(
            new DesktopPlatformCapabilities(processRunner, window));

        return services;
    }
}
