using EggLedger.Web.Platform;
using EggLedger.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EggLedger.Desktop.Export;

/// <summary>
/// Swaps the browser <see cref="IDownloadService"/> (browser download via the
/// download.js shim) for the desktop save-to-disk sink. Call AFTER AddEggLedgerWeb
/// so this override wins (same RemoveAll-then-add pattern as the D2 storage and D3
/// platform swaps). The browser concrete <see cref="DownloadService"/> registration
/// is dropped too so the JS-shim path cannot be resolved on desktop.
/// </summary>
public static class DesktopExportRegistration
{
    public static IServiceCollection AddDesktopExportSink(this IServiceCollection services)
    {
        services.RemoveAll<IDownloadService>();
        services.RemoveAll<DownloadService>();
        services.AddScoped<IDownloadService>(sp =>
            new DesktopDownloadService(sp.GetRequiredService<IPlatformCapabilities>()));
        return services;
    }
}
