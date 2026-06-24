using EggLedger.Web.Data;
using EggLedger.Web.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EggLedger.Desktop.Update;

/// <summary>
/// Registers the live desktop updater, replacing the browser no-op
/// <see cref="IUpdateStatusProvider"/>. Call AFTER AddEggLedgerWeb so this override
/// wins. Registered as a singleton (the update flow spans the app lifetime).
/// </summary>
public static class UpdateRegistration {
    /// <summary>
    /// Swap the no-op update-status provider for the live <see cref="UpdateService"/>.
    /// </summary>
    /// <param name="services">DI container.</param>
    /// <param name="runningVersion">Resolves the running app version string.</param>
    /// <param name="exitAction">
    /// OLD-instance exit run after the new instance signals /ready so it can rename
    /// itself. Defaults to <see cref="Environment.Exit(int)"/>.
    /// </param>
    public static IServiceCollection AddDesktopUpdater(
        this IServiceCollection services, Func<string> runningVersion, Func<Task>? exitAction = null) {
        var exit = exitAction ?? (() => {
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
            // Back the cooldown snapshot with the SQLite settings store (D2).
            // Constructing IndexedDbSettings here keeps the singleton updater off the
            // scoped IndexedDbSettings.
            settings: new IndexedDbSettings(sp.GetRequiredService<IIndexedDb>())));

        return services;
    }
}
