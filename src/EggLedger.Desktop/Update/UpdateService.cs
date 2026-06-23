using System.Globalization;
using System.Runtime.InteropServices;
using EggLedger.Desktop.Platform;
using EggLedger.Domain.Util;
using EggLedger.Web.Data;
using EggLedger.Web.Platform;

namespace EggLedger.Desktop.Update;

/// <summary>
/// Desktop self-update orchestrator and the live <see cref="IUpdateStatusProvider"/>
/// the About overlay binds to. Ties together the pure pieces:
/// <list type="bullet">
/// <item>version compare (<see cref="SemVersion"/>) for the "is a newer version
/// available" decision, ported from EggLedger/update/version.go CheckForUpdates;</item>
/// <item>the GitHub release fetch + binary download (<see cref="GithubReleaseClient"/>);</item>
/// <item>the token handshake (<see cref="HandshakeListener"/> /
/// <see cref="HandshakeClient"/>);</item>
/// <item>the self-rename + old-exit + stale cleanup (<see cref="BinaryReplacement"/>).</item>
/// </list>
///
/// MANUAL-VERIFY: the full self-replace (download the new binary, launch it as
/// EggLedger_new, hand off over loopback, the new instance renames itself, the old
/// instance exits after a short delay) is irreducibly cross-process and cannot be
/// unit-tested. The download, the handshake token check, the version decision, and
/// the rename/cleanup file moves ARE tested individually. The end-to-end flow is a
/// manual handoff on Windows.
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

    private UpdatePhase _phase = UpdatePhase.UpToDate;
    private string? _availableVersion;
    private string? _releaseNotes;
    private long _downloaded;
    private long _total;
    private string? _message;

    /// <param name="exitAction">
    /// The OLD-instance exit invoked once the new instance has reported in (or the
    /// handshake wait timed out). In the desktop host this exits the process so the
    /// new instance can rename EggLedger_new -&gt; EggLedger. Injected so the handoff
    /// has no untested side effect; tests pass a fake. Defaults to a no-op.
    /// </param>
    /// <param name="settings">
    /// Settings store backing the 12h cooldown snapshot (the cached latest tag + the
    /// last-check timestamp, same keys/format the Go host persists). When null the
    /// cooldown is disabled and every check polls (matching the pre-snapshot behavior);
    /// the live desktop host supplies the SQLite-backed store.
    /// </param>
    /// <param name="now">
    /// Wall-clock source for the cooldown decision (ports Go <c>time.Now()</c>).
    /// Injected so the cooldown is deterministically unit-testable; defaults to the
    /// real UTC clock.
    /// </param>
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
    /// Build the argv passed to the freshly launched EggLedger_new instance. Pure +
    /// testable; mirrors the args HandleDownloadAndInstall constructs
    /// (--replace-pid, --replace-path, and the handshake pair when a listener is up).
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

    public UpdatePhase Phase => _phase;
    public string? AvailableVersion => _availableVersion;
    public string? ReleaseNotes => _releaseNotes;
    public long DownloadedBytes => _downloaded;
    public long TotalBytes => _total;
    public string? Message => _message;

    public event Action? Changed;

    /// <summary>
    /// Poll GitHub and decide whether a newer version is available. Ports
    /// CheckForUpdates including the 12h cooldown + stored snapshot from the Go host:
    /// <list type="number">
    /// <item>parse the running version;</item>
    /// <item>if not forced and the stored <c>known_latest_version</c> already parses
    /// newer than running, skip the remote poll and report Available from the snapshot;</item>
    /// <item>if not forced and less than <see cref="UpdateCheckInterval"/> has elapsed
    /// since <c>last_update_check_at</c>, skip the poll entirely (stay UpToDate);</item>
    /// <item>otherwise fetch latest stable (and pre-releases when running is newer),
    /// persist the fetched tag + now to the snapshot, and report Available iff
    /// running &lt; latest.</item>
    /// </list>
    /// </summary>
    /// <param name="force">
    /// Bypass the cooldown and always poll (ports Go <c>_forceUpdateCheck</c> /
    /// <c>--force-update-check</c>). The About-overlay user check passes true; the
    /// automatic startup check passes false.
    /// </param>
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
            _availableVersion = snapshot.KnownTag;
            _releaseNotes = snapshot.KnownNotes;
            _message = null;
            SetPhase(UpdatePhase.Available);
            return;
        }

        // Within the cooldown and not forced: skip the network check entirely. The
        // stored known-latest was not newer (handled above), so we are up to date.
        if (!force && snapshot.LastCheckedAt is { } last && _now() - last < UpdateCheckInterval)
        {
            _availableVersion = null;
            _releaseNotes = null;
            _message = null;
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

        // Persist the snapshot + bump the last-check timestamp. Mirrors Go
        // _host.SetUpdateCheck(latestTag, latestReleaseNotes), which writes
        // known_latest_version, known_latest_release_notes, and last_update_check_at.
        await WriteSnapshotAsync(latestTag, latestNotes).ConfigureAwait(false);

        if (running.LessThan(latestVersion))
        {
            _availableVersion = latestTag;
            _releaseNotes = latestNotes;
            _message = null;
            SetPhase(UpdatePhase.Available);
        }
        else
        {
            _availableVersion = null;
            _releaseNotes = null;
            _message = null;
            SetPhase(UpdatePhase.UpToDate);
        }
    }

    /// <summary>The cooldown snapshot read from the settings store.</summary>
    private readonly record struct UpdateCheckSnapshot(
        DateTimeOffset? LastCheckedAt, string? KnownTag, string? KnownNotes);

    /// <summary>
    /// Read the stored cooldown snapshot (last-check timestamp + cached latest tag +
    /// notes). Mirrors the Go host UpdateCheckSnapshot. Returns an empty snapshot when
    /// no settings store is wired or a value is missing/unparseable, so a fresh data
    /// dir always polls.
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
    /// Persist the fetched latest tag + notes and stamp the last-check time to now.
    /// Mirrors Go SetUpdateCheck: writes <c>known_latest_version</c>,
    /// <c>known_latest_release_notes</c>, and <c>last_update_check_at</c> as an
    /// RFC3339Nano-compatible round-trip timestamp (matches the Go on-disk format).
    /// No-op when no settings store is wired.
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
    /// Download the release binary for <paramref name="tag"/> and begin the
    /// self-replace. Ports HandleDownloadAndInstall: download to the _new path, then
    /// kick off the OLD-instance handoff (start the handshake listener, launch the
    /// new instance with the token + replace flags, and on a successful /ready ping
    /// exit this process after the delay so the new instance can rename itself). The
    /// download + listener setup are testable in pieces; the live two-process
    /// choreography is MANUAL-VERIFY.
    /// </summary>
    public async Task DownloadAndInstallAsync(string tag)
    {
        _downloaded = 0;
        _total = 0;
        _message = null;
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

        // The Windows asset is the raw binary (downloaded straight to tempPath). The
        // linux asset is a .tar.gz and the mac assets are .zip; those are downloaded
        // to a temp archive then the binary is extracted + chmod'd into tempPath.
        // Mirrors the Windows-raw vs non-Windows-archive branch in
        // HandleDownloadAndInstall.
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

        _availableVersion = tag;
        SetPhase(UpdatePhase.Ready);

        // Hand off to the freshly downloaded binary. Matches the Go order in
        // HandleDownloadAndInstall: start the handshake listener, launch the new
        // instance with the token, wait for /ready, then exit the old instance after
        // the delay. The handoff blocks on the handshake + exit delay, so run it off
        // the caller's path (the Go binding likewise returns and lets the select-on-
        // HandoffChan goroutine exit the old instance).
        await RunSelfReplaceHandoffAsync(_exitAction, _handshakeTimeout, _exitDelay).ConfigureAwait(false);
    }

    /// <summary>
    /// MANUAL-VERIFY HANDOFF. Launch the downloaded binary as the NEW instance, then
    /// the OLD instance (this one) hands off and exits so the new instance can rename
    /// EggLedger_new -&gt; EggLedger. This is the irreducibly cross-process self-replace:
    /// it starts a second process, opens a loopback handshake listener, waits for the
    /// new instance's /ready ping, and exits after <see cref="OldExitDelay"/>. The
    /// constituent pieces (<see cref="BuildReplaceArgs"/>, the handshake token check,
    /// the rename, the exit delay) are unit-tested; the live two-process choreography
    /// is verified by hand on Windows and is NOT exercised by the test suite. Returns
    /// false if the launch failed.
    /// </summary>
    /// <param name="exitAction">
    /// Invoked once the new instance reports in (or the wait times out). In the real
    /// host this exits the process; injected so the method has no untested side effect.
    /// </param>
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

    /// <summary>
    /// Path of the freshly downloaded binary: &lt;exe&gt;_new (+ .exe on Windows).
    /// Mirrors the tempBinName logic in HandleDownloadAndInstall.
    /// </summary>
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
        _downloaded = downloaded;
        _total = total;
        Changed?.Invoke();
    }

    private void SetPhase(UpdatePhase phase)
    {
        _phase = phase;
        Changed?.Invoke();
    }

    private void Fail(string message)
    {
        _message = message;
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
