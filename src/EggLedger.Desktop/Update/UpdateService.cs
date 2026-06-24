using System.Globalization;
using System.Runtime.InteropServices;
using EggLedger.Desktop.Platform;
using EggLedger.Domain.Util;
using EggLedger.Web.Data;
using EggLedger.Web.Platform;

namespace EggLedger.Desktop.Update;

/// <summary>
/// Desktop self-update orchestrator and the live <see cref="IUpdateStatusProvider"/>
/// the About overlay binds to. Ties together version compare, GitHub fetch +
/// download, the token handshake, and the self-rename/cleanup.
/// MANUAL-VERIFY: the full cross-process self-replace cannot be unit-tested; the
/// individual pieces (download, token check, version decision, file moves) are.
/// </summary>
public sealed class UpdateService : IUpdateStatusProvider
{
    /// <summary>How long the old instance waits before exiting after handoff. Matches Go (5s).</summary>
    public static readonly TimeSpan OldExitDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The update-check cooldown. Matches Go <c>_updateCheckInterval</c> (12h): within
    /// this window an unforced check skips the GitHub poll and decides from the stored
    /// snapshot.
    /// </summary>
    public static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromHours(12);

    /// <summary>Settings key for the last-check timestamp. Matches Go (RFC3339Nano string).</summary>
    public const string LastUpdateCheckAtKey = "last_update_check_at";

    /// <summary>Settings key for the cached latest tag. Matches Go.</summary>
    public const string KnownLatestVersionKey = "known_latest_version";

    /// <summary>Settings key for the cached latest release notes. Matches Go.</summary>
    public const string KnownLatestReleaseNotesKey = "known_latest_release_notes";

    private readonly GithubReleaseClient _github;
    private readonly IProcessRunner _processRunner;
    private readonly Func<string> _runningVersion;
    private readonly Func<string?> _exePath;
    private readonly Func<Task> _exitAction;
    private readonly TimeSpan _handshakeTimeout;
    private readonly TimeSpan _exitDelay;
    private readonly IndexedDbSettings? _settings;
    private readonly Func<DateTimeOffset> _now;

    /// <param name="exitAction">
    /// OLD-instance exit run once the new instance reports in (or the wait times out),
    /// letting it rename EggLedger_new -&gt; EggLedger. Injected for testing; defaults
    /// to a no-op.
    /// </param>
    /// <param name="settings">
    /// Store backing the 12h cooldown snapshot. Null disables the cooldown so every
    /// check polls; the live host supplies the SQLite-backed store.
    /// </param>
    /// <param name="now">Wall clock for the cooldown decision (injected for testing).</param>
    public UpdateService(
        GithubReleaseClient github,
        Func<string> runningVersion,
        Func<string?>? exePath = null,
        IProcessRunner? processRunner = null,
        Func<Task>? exitAction = null,
        TimeSpan? handshakeTimeout = null,
        TimeSpan? exitDelay = null,
        IndexedDbSettings? settings = null,
        Func<DateTimeOffset>? now = null)
    {
        _github = github;
        _runningVersion = runningVersion;
        _exePath = exePath ?? (() => Environment.ProcessPath);
        _processRunner = processRunner ?? new ProcessRunner();
        _exitAction = exitAction ?? (() => Task.CompletedTask);
        _handshakeTimeout = handshakeTimeout ?? TimeSpan.FromSeconds(90);
        _exitDelay = exitDelay ?? OldExitDelay;
        _settings = settings;
        _now = now ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Build the argv passed to the launched EggLedger_new instance: --replace-pid,
    /// --replace-path, plus the handshake pair when a listener is up.
    /// </summary>
    public static IReadOnlyList<string> BuildReplaceArgs(int oldPid, string oldPath, string? handshakeAddr, string? handshakeToken)
    {
        var args = new List<string>
        {
            $"--replace-pid={oldPid.ToString(CultureInfo.InvariantCulture)}",
            $"--replace-path={oldPath}",
        };
        if (!string.IsNullOrEmpty(handshakeAddr) && !string.IsNullOrEmpty(handshakeToken))
        {
            args.Add($"--handshake-port={handshakeAddr}");
            args.Add($"--handshake-token={handshakeToken}");
        }
        return args;
    }

    public UpdatePhase Phase { get; private set; } = UpdatePhase.UpToDate;
    public string? AvailableVersion { get; private set; }
    public string? ReleaseNotes { get; private set; }
    public long DownloadedBytes { get; private set; }
    public long TotalBytes { get; private set; }
    public string? Message { get; private set; }

    public event Action? Changed;

    /// <summary>
    /// Poll GitHub and decide whether a newer version is available, with the 12h
    /// cooldown + stored snapshot: a known-newer stored tag reports Available without
    /// polling; within the cooldown an unforced check stays UpToDate; otherwise fetch
    /// latest (and pre-releases when running is newer), persist the snapshot, and
    /// report Available iff running &lt; latest.
    /// </summary>
    /// <param name="force">Bypass the cooldown and always poll (About-overlay check passes true).</param>
    public async Task CheckForUpdatesAsync(bool force = false)
    {
        SetPhase(UpdatePhase.Checking);

        if (!SemVersion.TryParse(_runningVersion(), out var running) || running is null)
        {
            Fail("could not parse running version");
            return;
        }

        var snapshot = await ReadSnapshotAsync().ConfigureAwait(false);

        // A known newer version is already stored: skip the remote poll and report it.
        // Mirrors the first branch of Go CheckForUpdates.
        if (!force && !string.IsNullOrEmpty(snapshot.KnownTag)
            && SemVersion.TryParse(snapshot.KnownTag, out var knownVersion) && knownVersion is not null
            && knownVersion.GreaterThan(running))
        {
            AvailableVersion = snapshot.KnownTag;
            ReleaseNotes = snapshot.KnownNotes;
            Message = null;
            SetPhase(UpdatePhase.Available);
            return;
        }

        // Within the cooldown and not forced: skip the network check entirely. The
        // stored known-latest was not newer (handled above), so we are up to date.
        if (!force && snapshot.LastCheckedAt is { } last && _now() - last < UpdateCheckInterval)
        {
            AvailableVersion = null;
            ReleaseNotes = null;
            Message = null;
            SetPhase(UpdatePhase.UpToDate);
            return;
        }

        var latest = await _github.GetLatestTagAsync().ConfigureAwait(false);
        if (latest is null)
        {
            Fail("could not fetch latest release");
            return;
        }

        var latestTag = latest.Value.Tag;
        var latestNotes = latest.Value.Body;
        if (!SemVersion.TryParse(latestTag, out var latestVersion) || latestVersion is null)
        {
            Fail($"could not parse latest version {latestTag}");
            return;
        }

        // If running a version newer than the latest stable (a pre-release build),
        // look at pre-releases for the true latest.
        if (running.GreaterThan(latestVersion))
        {
            var pre = await _github.GetLatestTagIncludingPreReleasesAsync().ConfigureAwait(false);
            if (pre is not null && SemVersion.TryParse(pre.Value.Tag, out var preVersion) && preVersion is not null)
            {
                latestTag = pre.Value.Tag;
                latestNotes = pre.Value.Body;
                latestVersion = preVersion;
            }
        }

        // Persist the snapshot + bump the last-check timestamp.
        await WriteSnapshotAsync(latestTag, latestNotes).ConfigureAwait(false);

        if (running.LessThan(latestVersion))
        {
            AvailableVersion = latestTag;
            ReleaseNotes = latestNotes;
            Message = null;
            SetPhase(UpdatePhase.Available);
        }
        else
        {
            AvailableVersion = null;
            ReleaseNotes = null;
            Message = null;
            SetPhase(UpdatePhase.UpToDate);
        }
    }

    /// <summary>The cooldown snapshot read from the settings store.</summary>
    private readonly record struct UpdateCheckSnapshot(
        DateTimeOffset? LastCheckedAt, string? KnownTag, string? KnownNotes);

    /// <summary>
    /// Read the stored cooldown snapshot. Returns empty when no store is wired or a
    /// value is missing/unparseable, so a fresh data dir always polls.
    /// </summary>
    private async Task<UpdateCheckSnapshot> ReadSnapshotAsync()
    {
        if (_settings is null)
        {
            return default;
        }

        var all = await _settings.GetAllSettingsAsync().ConfigureAwait(false);
        DateTimeOffset? lastCheckedAt = null;
        if (all.TryGetValue(LastUpdateCheckAtKey, out var rawLast) && !string.IsNullOrEmpty(rawLast)
            && DateTimeOffset.TryParse(rawLast, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var parsed))
        {
            lastCheckedAt = parsed;
        }

        all.TryGetValue(KnownLatestVersionKey, out var knownTag);
        all.TryGetValue(KnownLatestReleaseNotesKey, out var knownNotes);
        return new UpdateCheckSnapshot(lastCheckedAt, knownTag, knownNotes);
    }

    /// <summary>
    /// Persist the fetched tag + notes and stamp last-check to now (round-trip
    /// timestamp matching the Go on-disk format). No-op when no store is wired.
    /// </summary>
    private async Task WriteSnapshotAsync(string latestTag, string latestNotes)
    {
        if (_settings is null)
        {
            return;
        }

        await _settings.SetSettingsAsync(new Dictionary<string, string>
        {
            [LastUpdateCheckAtKey] = _now().ToString("O", CultureInfo.InvariantCulture),
            [KnownLatestVersionKey] = latestTag,
            [KnownLatestReleaseNotesKey] = latestNotes,
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Download the release binary for tag to the _new path, then begin the
    /// OLD-instance handoff. MANUAL-VERIFY: the live two-process choreography is not
    /// unit-tested.
    /// </summary>
    public async Task DownloadAndInstallAsync(string tag)
    {
        DownloadedBytes = 0;
        TotalBytes = 0;
        Message = null;
        SetPhase(UpdatePhase.Downloading);

        var exePath = _exePath();
        if (string.IsNullOrEmpty(exePath))
        {
            Fail("could not resolve running executable path");
            return;
        }

        var assetUrl = await _github.GetUpdateAssetUrlAsync(tag).ConfigureAwait(false);
        if (assetUrl is null)
        {
            Fail($"no release asset for this platform in {tag}");
            return;
        }

        var tempPath = NewBinaryTempPath(exePath);
        TryDelete(tempPath);

        // Windows asset is the raw binary (straight to tempPath). Linux .tar.gz / mac
        // .zip assets download to a temp archive, then extract + chmod into tempPath.
        var assetName = GithubReleaseClient.ExpectedAssetName();
        if (ArchiveExtraction.IsArchive(assetName))
        {
            var archivePath = Path.Combine(Path.GetTempPath(), assetName);
            TryDelete(archivePath);
            try
            {
                await _github.DownloadAsync(assetUrl, archivePath, OnProgress).ConfigureAwait(false);
                ArchiveExtraction.Extract(archivePath, tempPath);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException or InvalidOperationException)
            {
                TryDelete(archivePath);
                TryDelete(tempPath);
                Fail($"download failed: {ex.Message}");
                return;
            }
            TryDelete(archivePath);
        }
        else
        {
            try
            {
                await _github.DownloadAsync(assetUrl, tempPath, OnProgress).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
            {
                TryDelete(tempPath);
                Fail($"download failed: {ex.Message}");
                return;
            }
        }

        AvailableVersion = tag;
        SetPhase(UpdatePhase.Ready);

        // Hand off: start the listener, launch the new instance with the token, wait
        // for /ready, then exit after the delay. Blocks on the handshake + exit delay.
        await RunSelfReplaceHandoffAsync(_exitAction, _handshakeTimeout, _exitDelay).ConfigureAwait(false);
    }

    /// <summary>
    /// MANUAL-VERIFY: launch the downloaded binary as the NEW instance, open a
    /// loopback handshake listener, wait for its /ready ping, and exit after
    /// <see cref="OldExitDelay"/> so it can rename itself. Returns false if the launch
    /// failed. The live two-process choreography is not unit-tested.
    /// </summary>
    /// <param name="exitAction">Run once the new instance reports in or the wait times out (injected for testing).</param>
    public async Task<bool> RunSelfReplaceHandoffAsync(
        Func<Task> exitAction,
        TimeSpan? handshakeTimeout = null,
        TimeSpan? exitDelay = null)
    {
        var hsTimeout = handshakeTimeout ?? TimeSpan.FromSeconds(90);
        var oldExitDelay = exitDelay ?? OldExitDelay;
        var exePath = _exePath();
        if (string.IsNullOrEmpty(exePath))
        {
            return false;
        }
        var tempPath = NewBinaryTempPath(exePath);
        if (!File.Exists(tempPath))
        {
            return false;
        }

        var token = HandshakeToken.New();
        HandshakeListener? listener = null;
        try
        {
            listener = HandshakeListener.Start(token);
        }
        catch (Exception ex) when (ex is System.Net.HttpListenerException or System.Net.Sockets.SocketException)
        {
            // No listener; fall back to a name/pid-based hand-off.
        }

        var args = BuildReplaceArgs(Environment.ProcessId, exePath, listener?.Address, token);
        try
        {
            await _processRunner.RunAsync(tempPath, args).ConfigureAwait(false);
        }
        catch
        {
            listener?.Dispose();
            return false;
        }

        if (listener is not null)
        {
            await Task.WhenAny(listener.Served, Task.Delay(hsTimeout)).ConfigureAwait(false);
            listener.Dispose();
        }

        // Give the new instance the exit delay to settle, then exit so it can rename.
        await Task.Delay(oldExitDelay).ConfigureAwait(false);
        await exitAction().ConfigureAwait(false);
        return true;
    }

    /// <summary>Path of the downloaded binary: &lt;exe&gt;_new (+ .exe on Windows).</summary>
    public static string NewBinaryTempPath(string exePath)
    {
        var dir = Path.GetDirectoryName(exePath) ?? "";
        var name = Path.GetFileName(exePath);
        var isExe = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
        if (isExe)
        {
            name = name[..^4];
        }
        name += BinaryNaming.NewBinarySuffix;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            name += ".exe";
        }
        return Path.Combine(dir, name);
    }

    private void OnProgress(long downloaded, long total)
    {
        DownloadedBytes = downloaded;
        TotalBytes = total;
        Changed?.Invoke();
    }

    private void SetPhase(UpdatePhase phase)
    {
        Phase = phase;
        Changed?.Invoke();
    }

    private void Fail(string message)
    {
        Message = message;
        SetPhase(UpdatePhase.Failed);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
