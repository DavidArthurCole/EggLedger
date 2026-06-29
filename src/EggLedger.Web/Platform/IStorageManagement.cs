namespace EggLedger.Web.Platform;

/// <summary>Seam over desktop storage backup/move. Implemented by EggLedger.Desktop; the browser build is a no-op since the Storage panel is gated on <see cref="IPlatformCapabilities.IsDesktop"/>.</summary>
public interface IStorageManagement {
    string GetDataRootDir();

    /// <summary>Copies each selected part into destPath. Source files are never deleted.</summary>
    Task BackupAsync(string destPath, bool db, bool exports, bool logs);

    /// <summary>Copies all data to destPath, repoints the data root, then restarts.</summary>
    Task MoveAsync(string destPath);
}
