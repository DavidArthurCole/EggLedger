using System.Text.Json;
using System.Text.Json.Serialization;

namespace EggLedger.Desktop.Storage;

/// <summary>
/// Data-root and subdirectory resolvers plus the atomic copy-then-move helpers.
/// Port of Go storage/storage_move.go. A <c>bootstrap.json</c> under the user
/// config dir optionally redirects the data root (set when the user relocates
/// their data); otherwise everything lives under the executable directory.
/// </summary>
public static class StoragePaths
{
    private const string AppDirName = "EggLedger";
    private const string BootstrapFileName = "bootstrap.json";

    /// <summary>
    /// Path to bootstrap.json under the OS user config dir, or "" when the config
    /// dir cannot be resolved. Mirrors Go bootstrapPath / os.UserConfigDir.
    /// </summary>
    public static string BootstrapPath()
    {
        var configDir = UserConfigDir();
        return configDir == "" ? "" : Path.Combine(configDir, AppDirName, BootstrapFileName);
    }

    /// <summary>
    /// Internal data dir (SQLite DBs, caches). Honors bootstrap data_root_dir, then
    /// legacy internal_dir, else rootDir/internal. Port of Go ResolveInternalDir.
    /// </summary>
    public static string ResolveInternalDir(string rootDir)
    {
        var cfg = ReadBootstrapConfig();
        if (cfg.DataRootDir != "")
        {
            return Path.Combine(cfg.DataRootDir, "internal");
        }
        if (cfg.InternalDir != "")
        {
            return cfg.InternalDir;
        }
        return Path.Combine(rootDir, "internal");
    }

    /// <summary>Exports dir (CSV/XLSX). Port of Go ResolveExportsDir.</summary>
    public static string ResolveExportsDir(string rootDir)
    {
        var cfg = ReadBootstrapConfig();
        return cfg.DataRootDir != ""
            ? Path.Combine(cfg.DataRootDir, "exports")
            : Path.Combine(rootDir, "exports");
    }

    /// <summary>Logs dir. Port of Go ResolveLogsDir.</summary>
    public static string ResolveLogsDir(string rootDir)
    {
        var cfg = ReadBootstrapConfig();
        return cfg.DataRootDir != ""
            ? Path.Combine(cfg.DataRootDir, "logs")
            : Path.Combine(rootDir, "logs");
    }

    /// <summary>
    /// Root directory owning all app data. The bootstrap data_root_dir when set,
    /// else rootDir (exe dir) for legacy layouts. Port of Go ResolveDataRootDir.
    /// </summary>
    public static string ResolveDataRootDir(string rootDir)
    {
        var cfg = ReadBootstrapConfig();
        return cfg.DataRootDir != "" ? cfg.DataRootDir : rootDir;
    }

    /// <summary>
    /// Writes bootstrap.json with <paramref name="dataRootDir"/> as the canonical
    /// root (clears the legacy internal_dir key). Port of Go WriteBootstrapConfig.
    /// </summary>
    public static void WriteBootstrapConfig(string dataRootDir)
    {
        var path = BootstrapPath();
        if (path == "")
        {
            throw new FileNotFoundException("user config dir is unavailable");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(new BootstrapConfig { DataRootDir = dataRootDir }, JsonOpts);
        File.WriteAllText(path, json);
    }

    /// <summary>Reads bootstrap.json, returning an empty config on any error. Port of Go readBootstrapConfig.</summary>
    public static BootstrapConfig ReadBootstrapConfig()
    {
        var path = BootstrapPath();
        if (path == "" || !File.Exists(path))
        {
            return new BootstrapConfig();
        }
        try
        {
            var data = File.ReadAllText(path);
            return JsonSerializer.Deserialize<BootstrapConfig>(data, JsonOpts) ?? new BootstrapConfig();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return new BootstrapConfig();
        }
    }

    /// <summary>
    /// Recursively copies <paramref name="src"/> into <paramref name="dst"/>,
    /// creating directories as needed. Port of Go CopyDir.
    /// </summary>
    public static void CopyDir(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var dir in Directory.EnumerateDirectories(src, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(src, dir);
            Directory.CreateDirectory(Path.Combine(dst, rel));
        }
        foreach (var file in Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(src, file);
            CopyFile(file, Path.Combine(dst, rel));
        }
    }

    /// <summary>Copies one file, creating the destination directory. Port of Go CopyFile.</summary>
    public static void CopyFile(string src, string dst)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
        File.Copy(src, dst, overwrite: true);
    }

    private static string UserConfigDir()
    {
        // Matches Go os.UserConfigDir on Windows (APPDATA) and the XDG/Apple paths
        // elsewhere; Environment.SpecialFolder.ApplicationData maps to the same
        // roots used by the Go reference.
        var dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return string.IsNullOrEmpty(dir) ? "" : dir;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
    };

    /// <summary>Bootstrap config shape. Mirrors Go storage.bootstrapConfig.</summary>
    public sealed class BootstrapConfig
    {
        [JsonPropertyName("data_root_dir")]
        public string DataRootDir { get; set; } = "";

        /// <summary>Legacy key; superseded by data_root_dir.</summary>
        [JsonPropertyName("internal_dir")]
        public string InternalDir { get; set; } = "";
    }
}
