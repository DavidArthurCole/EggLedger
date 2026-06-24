using System.Text.Json;
using System.Text.Json.Serialization;

namespace EggLedger.Desktop.Update;

/// <summary>
/// Outcome payload the replace-mode process writes next to the exe and the
/// relaunched process reads once on startup. Lives in the exe dir (not the data
/// root) so both processes agree on the path with no init dependency.
/// </summary>
public sealed class UpdateStatus {
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Message { get; init; }

    [JsonPropertyName("toVersion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? ToVersion { get; init; }

    [JsonPropertyName("newBinary")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? NewBinary { get; init; }
}

/// <summary>
/// Reads/writes the on-disk update-status handoff file. Pure file IO over an
/// injected directory, so it is unit-testable with a temp dir.
/// </summary>
public static class UpdateStatusFile {
    /// <summary>File name written next to the exe. Matches Go.</summary>
    public const string FileName = ".egg-update-status.json";

    private static readonly JsonSerializerOptions JsonOptions = new() {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
    };

    private static string PathFor(string dir) => Path.Combine(dir, FileName);

    /// <summary>Write the status JSON into <paramref name="dir"/>.</summary>
    public static void Write(string dir, UpdateStatus status) {
        var data = JsonSerializer.Serialize(status, JsonOptions);
        File.WriteAllText(PathFor(dir), data);
    }

    /// <summary>
    /// Read and delete the status file. Returns null when absent or unreadable
    /// (matching the Go (nil, false) contract). The file is removed before parse
    /// so a malformed file is not re-read.
    /// </summary>
    public static UpdateStatus? ReadAndClear(string dir) {
        var path = PathFor(dir);
        string data;
        try {
            data = File.ReadAllText(path);
        } catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or IOException or UnauthorizedAccessException) {
            return null;
        }

        try {
            File.Delete(path);
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            // Best-effort delete, matching Go's ignored os.Remove error.
        }

        try {
            return JsonSerializer.Deserialize<UpdateStatus>(data, JsonOptions);
        } catch (JsonException) {
            return null;
        }
    }
}
