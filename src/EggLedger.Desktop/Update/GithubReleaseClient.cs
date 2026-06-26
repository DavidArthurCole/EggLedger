using System.Runtime.InteropServices;
using System.Text.Json;

namespace EggLedger.Desktop.Update;

/// <summary>
/// GitHub releases API client + asset download. HttpClient is injected so URL
/// construction, JSON parse, and byte write are unit-testable with a stubbed handler.
/// </summary>
public sealed class GithubReleaseClient(HttpClient httpClient) {
    /// <summary>Repo the updater polls. Matches Go _githubRepo.</summary>
    public const string GithubRepo = "DavidArthurCole/EggLedger";

    private readonly HttpClient _httpClient = httpClient;

    /// <summary>A release tag plus its body (notes).</summary>
    public readonly record struct Release(string Tag, string Body);

    /// <summary>
    /// Fetch the latest stable release tag + notes (GET /releases/latest). Ports
    /// getLatestTag. Returns null when the tag is empty or the request fails.
    /// </summary>
    public async Task<Release?> GetLatestTagAsync(CancellationToken cancel = default) {
        var url = $"https://api.github.com/repos/{GithubRepo}/releases/latest";
        var json = await GetStringAsync(url, cancel).ConfigureAwait(false);
        if (json is null) {
            return null;
        }
        try {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
            if (string.IsNullOrEmpty(tag)) {
                return null;
            }
            var body = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
            return new Release(tag, body);
        } catch (JsonException) {
            return null;
        }
    }

    /// <summary>
    /// Fetch the highest non-draft release including pre-releases (GET /releases).
    /// Ports getLatestTagIncludingPreReleases. Returns null when none parse.
    /// </summary>
    public async Task<Release?> GetLatestTagIncludingPreReleasesAsync(CancellationToken cancel = default) {
        var url = $"https://api.github.com/repos/{GithubRepo}/releases?per_page=10";
        var json = await GetStringAsync(url, cancel).ConfigureAwait(false);
        if (json is null) {
            return null;
        }

        try {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) {
                return null;
            }
            EggLedger.Domain.Util.SemVersion? highest = null;
            Release? best = null;
            foreach (var rel in doc.RootElement.EnumerateArray()) {
                if (rel.TryGetProperty("draft", out var d) && d.ValueKind == JsonValueKind.True) {
                    continue;
                }
                var tag = rel.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
                if (string.IsNullOrEmpty(tag) || !EggLedger.Domain.Util.SemVersion.TryParse(tag, out var v) || v is null) {
                    continue;
                }
                if (highest is null || v.GreaterThan(highest)) {
                    highest = v;
                    var body = rel.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
                    best = new Release(tag, body);
                }
            }
            return best;
        } catch (JsonException) {
            return null;
        }
    }

    /// <summary>
    /// Release asset filename for the current platform. Ports expectedAssetName.
    /// </summary>
    public static string ExpectedAssetName() {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            return "EggLedger.exe";
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
            return "EggLedger-linux.tar.gz";
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            return RuntimeInformation.OSArchitecture == Architecture.Arm64
                ? "EggLedger-mac-arm64.zip"
                : "EggLedger-mac.zip";
        }
        return "EggLedger";
    }

    /// <summary>
    /// Resolve the browser_download_url for this platform's asset in release
    /// <paramref name="tag"/>. Ports getUpdateAssetURL; null when the asset is missing.
    /// </summary>
    public async Task<string?> GetUpdateAssetUrlAsync(string tag, CancellationToken cancel = default) {
        var url = $"https://api.github.com/repos/{GithubRepo}/releases/tags/{tag}";
        var json = await GetStringAsync(url, cancel).ConfigureAwait(false);
        if (json is null) {
            return null;
        }

        try {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) {
                return null;
            }
            var want = ExpectedAssetName();
            foreach (var asset in assets.EnumerateArray()) {
                var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (name == want) {
                    var dl = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                    return string.IsNullOrEmpty(dl) ? null : dl;
                }
            }
            return null;
        } catch (JsonException) {
            return null;
        }
    }

    /// <summary>
    /// Expected SHA-256 (lowercase hex) for this platform's asset, read from the release's
    /// SHA256SUMS asset (lines of "hash␠␠filename"). Null when the asset or its line is absent.
    /// </summary>
    public async Task<string?> GetExpectedSha256Async(string tag, CancellationToken cancel = default) {
        var listUrl = $"https://api.github.com/repos/{GithubRepo}/releases/tags/{tag}";
        var json = await GetStringAsync(listUrl, cancel).ConfigureAwait(false);
        if (json is null) {
            return null;
        }
        string? sumsUrl = null;
        try {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) {
                return null;
            }
            foreach (var asset in assets.EnumerateArray()) {
                if ((asset.TryGetProperty("name", out var n) ? n.GetString() : null) == "SHA256SUMS") {
                    sumsUrl = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                    break;
                }
            }
        } catch (JsonException) {
            return null;
        }
        if (string.IsNullOrEmpty(sumsUrl)) {
            return null;
        }

        var sums = await GetStringAsync(sumsUrl, cancel).ConfigureAwait(false);
        if (sums is null) {
            return null;
        }
        var want = ExpectedAssetName();
        foreach (var line in sums.Split('\n')) {
            var trimmed = line.Trim();
            if (trimmed.Length == 0) {
                continue;
            }
            var parts = trimmed.Split([' '], 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && parts[1].Trim().TrimStart('*') == want) {
                return parts[0].Trim().ToLowerInvariant();
            }
        }
        return null;
    }

    /// <summary>
    /// Stream the asset to <paramref name="destPath"/>, reporting (downloaded, total)
    /// roughly every 64KB. Ports downloadUpdate; throws on non-success or truncated download.
    /// </summary>
    public async Task DownloadAsync(
        string assetUrl,
        string destPath,
        Action<long, long>? progress,
        CancellationToken cancel = default) {
        using var resp = await _httpClient.GetAsync(assetUrl, HttpCompletionOption.ResponseHeadersRead, cancel)
            .ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        var total = resp.Content.Headers.ContentLength ?? 0;
        await using var src = await resp.Content.ReadAsStreamAsync(cancel).ConfigureAwait(false);
        await using var dst = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
        var buffer = new byte[64 * 1024];
        long downloaded = 0;
        long lastReport = 0;
        int read;
        while ((read = await src.ReadAsync(buffer, cancel).ConfigureAwait(false)) > 0) {
            await dst.WriteAsync(buffer.AsMemory(0, read), cancel).ConfigureAwait(false);
            downloaded += read;
            if (downloaded - lastReport >= 64 * 1024) {
                progress?.Invoke(downloaded, total);
                lastReport = downloaded;
            }
        }
        progress?.Invoke(downloaded, total);
        await dst.FlushAsync(cancel).ConfigureAwait(false);

        if (total > 0 && downloaded != total) {
            throw new IOException($"incomplete download: received {downloaded} of {total} bytes");
        }
    }

    private async Task<string?> GetStringAsync(string url, CancellationToken cancel) {
        try {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            // GitHub requires a User-Agent; the Go default client sends one implicitly.
            if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0) {
                req.Headers.UserAgent.ParseAdd("EggLedger");
            }
            using var resp = await _httpClient.SendAsync(req, cancel).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) {
                return null;
            }
            return await resp.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);
        } catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) {
            return null;
        }
    }
}
