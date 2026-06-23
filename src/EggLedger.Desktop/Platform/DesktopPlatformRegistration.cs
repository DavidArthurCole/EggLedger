using EggLedger.Web.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Photino.NET;

namespace EggLedger.Desktop.Platform;

/// <summary>
/// Registers the native desktop <see cref="IPlatformCapabilities"/>, replacing the
/// browser no-op stub. Call AFTER AddEggLedgerWeb so this override wins (same
/// RemoveAll-then-add pattern as the SQLite storage swap). The Photino window is
/// only available after the app is built, so the window-backed services are wired
/// once the host has its MainWindow.
/// </summary>
public static class DesktopPlatformRegistration
{
    /// <summary>
    /// Swaps the browser <see cref="IPlatformCapabilities"/> for the native desktop
    /// impl over <paramref name="window"/>. Registers the process runner and window
    /// wrapper as singletons (one window for the app lifetime).
    /// </summary>
    public static IServiceCollection AddDesktopPlatformCapabilities(
        this IServiceCollection services, PhotinoWindow window)
        => services.AddDesktopPlatformCapabilities(new ProcessRunner(), new PhotinoDesktopWindow(window));

    /// <summary>
    /// Registers the native capabilities over explicit seams. Used by the window
    /// overload above and directly by tests with fakes.
    /// </summary>
    public static IServiceCollection AddDesktopPlatformCapabilities(
        this IServiceCollection services, IProcessRunner processRunner, IDesktopWindow window)
    {
        services.AddSingleton(processRunner);
        services.AddSingleton(window);

        // Drop the browser no-op stub so the native impl is resolved everywhere.
        services.RemoveAll<IPlatformCapabilities>();
        services.AddSingleton<IPlatformCapabilities>(
            new DesktopPlatformCapabilities(processRunner, window));

        return services;
    }
}
