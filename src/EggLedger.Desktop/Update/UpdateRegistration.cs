using EggLedger.Web.Data;
using EggLedger.Web.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EggLedger.Desktop.Update;

/// <summary>
/// Registers the live desktop updater, replacing the browser no-op
/// <see cref="IUpdateStatusProvider"/>. Call AFTER AddEggLedgerWeb so this override
/// wins (same RemoveAll-then-add pattern as the storage and platform-capability
/// swaps). The provider is registered as a singleton because the update flow spans
/// the whole app lifetime and the About overlay subscribes to its Changed event.
/// </summary>
public static class UpdateRegistration
{
    /// <summary>
    /// Swap the no-op update-status provider for the live <see cref="UpdateService"/>.
    /// </summary>
    /// <param name="services">DI container.</param>
    /// <param name="runningVersion">Resolves the running app version string.</param>
    /// <param name="exitAction">
    /// The OLD-instance exit run after the new instance signals /ready, so the new
    /// instance can rename itself. In the desktop host this exits the process
    /// (matching the Go old-instance exit after HandoffChan). Defaults to
    /// <see cref="Environment.Exit(int)"/> when not supplied.
    /// </param>
    public static IServiceCollection AddDesktopUpdater(
        this IServiceCollection services, Func<string> runningVersion, Func<Task>? exitAction = null)
    {
        var exit = exitAction ?? (() =>
        {
            Environment.Exit(0);
            return Task.CompletedTask;
        });

        services.AddSingleton(sp => new GithubReleaseClient(
            sp.GetService<HttpClient>() ?? new HttpClient()));

        services.RemoveAll<IUpdateStatusProvider>();
        services.AddSingleton<IUpdateStatusProvider>(sp => new UpdateService(
            sp.GetRequiredService<GithubReleaseClient>(),
            runningVersion,
            exitAction: exit,
            // Back the 12h cooldown snapshot with the SQLite settings store (D2). The
            // IIndexedDb singleton is the native SQLite-backed store on desktop, and
            // IndexedDbSettings is a thin keyed wrapper over it, so constructing one
            // here keeps the singleton updater off the scoped IndexedDbSettings.
            settings: new IndexedDbSettings(sp.GetRequiredService<IIndexedDb>())));

        return services;
    }
}
