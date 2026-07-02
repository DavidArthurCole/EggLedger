using EggLedger.Web.Platform;
using EggLedger.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.JSInterop;

namespace EggLedger.Desktop.Export;

/// <summary>
/// Swaps the browser <see cref="IDownloadService"/> for the desktop save-to-disk sink;
/// call AFTER AddEggLedgerWeb so this wins. Drops concrete <see cref="DownloadService"/>
/// too so the JS-shim path cannot resolve.
/// </summary>
public static class DesktopExportRegistration {
    public static IServiceCollection AddDesktopExportSink(this IServiceCollection services) {
        services.RemoveAll<IDownloadService>();
        services.RemoveAll<DownloadService>();
        services.AddScoped<IDownloadService>(sp =>
            new DesktopDownloadService(sp.GetRequiredService<IPlatformCapabilities>(), sp.GetRequiredService<IJSRuntime>()));
        return services;
    }
}
