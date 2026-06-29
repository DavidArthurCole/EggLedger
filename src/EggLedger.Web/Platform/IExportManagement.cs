using EggLedger.Domain.Export;

namespace EggLedger.Web.Platform;

/// <summary>Seam over desktop export-file management. Implemented by EggLedger.Desktop; the browser build is a no-op since the panel is gated on <see cref="IPlatformCapabilities.IsDesktop"/>.</summary>
public interface IExportManagement {
    /// <summary>Groups newest-first, enriched with account nickname/color.</summary>
    Task<List<ExportGroup>> ListAsync();

    /// <summary>Prunes every group to at most keepCount newest pairs; returns aggregate counts.</summary>
    Task<(int deleted, long freedBytes)> PruneAsync(int keepCount);

    /// <summary>Deletes each path; missing files are ignored.</summary>
    Task DeleteAsync(IEnumerable<string> paths);
}
