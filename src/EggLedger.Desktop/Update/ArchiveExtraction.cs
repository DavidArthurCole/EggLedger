using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace EggLedger.Desktop.Update;

/// <summary>
/// Pulls the EggLedger binary out of a downloaded release archive. Windows assets are
/// raw; linux .tar.gz and mac .zip need extraction plus the unix executable bit before
/// launching as EggLedger_new.
/// </summary>
public static class ArchiveExtraction {
    /// <summary>Executable mode Go applies to the extracted binary (0755 / rwxr-xr-x).</summary>
    private const UnixFileMode ExecutableMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
        | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
        | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    /// <summary>
    /// True when the asset is a .tar.gz / .zip needing extraction, not the raw Windows
    /// binary. Mirrors the suffix branch in HandleDownloadAndInstall.
    /// </summary>
    public static bool IsArchive(string assetName) =>
        assetName.EndsWith(".tar.gz", StringComparison.Ordinal)
        || assetName.EndsWith(".zip", StringComparison.Ordinal);

    /// <summary>
    /// Extract the binary by suffix, then set the unix executable bit. Ports
    /// extractBinaryFromArchive + the following os.Chmod(tempPath, 0755).
    /// </summary>
    public static void Extract(string archivePath, string destPath) {
        if (archivePath.EndsWith(".tar.gz", StringComparison.Ordinal)) {
            ExtractFromTarGz(archivePath, destPath);
        } else if (archivePath.EndsWith(".zip", StringComparison.Ordinal)) {
            ExtractFromZip(archivePath, destPath);
        } else {
            throw new InvalidOperationException($"unsupported archive format: {Path.GetFileName(archivePath)}");
        }

        SetExecutableBit(destPath);
    }

    /// <summary>Extract the first regular non-empty file from a .tar.gz. Ports extractFromTarGz.</summary>
    private static void ExtractFromTarGz(string archivePath, string destPath) {
        using var file = File.OpenRead(archivePath);
        using var gz = new GZipStream(file, CompressionMode.Decompress);
        using var tar = new TarReader(gz);

        TarEntry? entry;
        while ((entry = tar.GetNextEntry()) is not null) {
            if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile)) {
                continue;
            }
            if (entry.Length <= 0 || entry.DataStream is null) {
                continue;
            }
            using var dst = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
            entry.DataStream.CopyTo(dst);
            return;
        }

        throw new InvalidOperationException("no regular file found in archive");
    }

    /// <summary>
    /// Extract from a .zip, preferring a MacOS/ app-bundle entry then the first
    /// extensionless file. Ports extractFromZip.
    /// </summary>
    private static void ExtractFromZip(string archivePath, string destPath) {
        using var zip = ZipFile.OpenRead(archivePath);

        ZipArchiveEntry? target = null;
        foreach (var entry in zip.Entries) {
            if (IsDirectoryEntry(entry)) {
                continue;
            }
            if (entry.FullName.Contains("/MacOS/", StringComparison.Ordinal)) {
                target = entry;
                break;
            }
        }

        if (target is null) {
            foreach (var entry in zip.Entries) {
                if (IsDirectoryEntry(entry)) {
                    continue;
                }
                if (Path.GetExtension(entry.FullName).Length == 0) {
                    target = entry;
                    break;
                }
            }
        }

        if (target is null) {
            throw new InvalidOperationException("no suitable binary found in zip archive");
        }

        using var src = target.Open();
        using var dst = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
        src.CopyTo(dst);
    }

    /// <summary>A zip directory entry has an empty name after its trailing slash.</summary>
    private static bool IsDirectoryEntry(ZipArchiveEntry entry) =>
        entry.FullName.EndsWith('/') || string.IsNullOrEmpty(entry.Name);

    /// <summary>
    /// Set the 0755 bit on unix; no-op on Windows where SetUnixFileMode throws. Matches
    /// Go's os.Chmod(tempPath, 0755).
    /// </summary>
    private static void SetExecutableBit(string path) {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            return;
        }
        File.SetUnixFileMode(path, ExecutableMode);
    }
}
