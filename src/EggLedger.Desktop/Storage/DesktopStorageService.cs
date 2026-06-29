using EggLedger.Web.Platform;

namespace EggLedger.Desktop.Storage;

public enum StoragePart {
    Internal,
    Exports,
    Logs,
}

/// <summary>
/// Backup and move of the desktop data tree. Port of Go main.go backupStoragePart /
/// moveStorageTo over StoragePaths copy helpers. Source files are never deleted.
/// </summary>
public sealed class DesktopStorageService : IStorageManagement {
    private readonly string _rootDir;
    private readonly IPlatformCapabilities _platform;
    private readonly Action<string> _writeBootstrap;

    public DesktopStorageService(string rootDir, IPlatformCapabilities platform)
        : this(rootDir, platform, StoragePaths.WriteBootstrapConfig) {
    }

    /// <summary>The bootstrap-write seam lets tests avoid touching the global user bootstrap.json.</summary>
    public DesktopStorageService(string rootDir, IPlatformCapabilities platform, Action<string> writeBootstrap) {
        _rootDir = rootDir;
        _platform = platform;
        _writeBootstrap = writeBootstrap;
    }

    public string GetDataRootDir() => StoragePaths.ResolveDataRootDir(_rootDir);

    /// <summary>Copies each selected part into destPath/{internal,exports,logs}. Missing source parts are skipped.</summary>
    public Task BackupAsync(string destPath, bool db, bool exports, bool logs) {
        return Task.Run(() => {
            if (db) {
                CopyPart(StoragePart.Internal, destPath);
            }
            if (exports) {
                CopyPart(StoragePart.Exports, destPath);
            }
            if (logs) {
                CopyPart(StoragePart.Logs, destPath);
            }
        });
    }

    /// <summary>Copies all parts to destPath, points the bootstrap data root there, then restarts.</summary>
    public async Task MoveAsync(string destPath) {
        if (PathsEqual(destPath, GetDataRootDir())) {
            throw new InvalidOperationException("destination is the current data root");
        }
        await Task.Run(() => CopyAllAndWriteBootstrap(destPath));
        await _platform.RestartAppAsync();
    }

    private void CopyAllAndWriteBootstrap(string destPath) {
        CopyPart(StoragePart.Internal, destPath);
        CopyPart(StoragePart.Exports, destPath);
        CopyPart(StoragePart.Logs, destPath);
        _writeBootstrap(destPath);
    }

    private void CopyPart(StoragePart part, string destPath) {
        var source = SourceDir(part);
        if (!Directory.Exists(source)) {
            return;
        }
        StoragePaths.CopyDir(source, Path.Combine(destPath, SubDirName(part)));
    }

    private string SourceDir(StoragePart part) => part switch {
        StoragePart.Internal => StoragePaths.ResolveInternalDir(_rootDir),
        StoragePart.Exports => StoragePaths.ResolveExportsDir(_rootDir),
        StoragePart.Logs => StoragePaths.ResolveLogsDir(_rootDir),
        _ => throw new ArgumentOutOfRangeException(nameof(part)),
    };

    private static string SubDirName(StoragePart part) => part switch {
        StoragePart.Internal => "internal",
        StoragePart.Exports => "exports",
        StoragePart.Logs => "logs",
        _ => throw new ArgumentOutOfRangeException(nameof(part)),
    };

    private static bool PathsEqual(string a, string b) {
        var comparison = OperatingSystem.IsLinux()
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
            comparison);
    }
}
