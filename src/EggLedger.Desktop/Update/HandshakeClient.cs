namespace EggLedger.Desktop.Update;

/// <summary>
/// New instance's side of the handshake: POST /ready?token to the old instance,
/// best-effort with a few quick retries until a 200.
/// </summary>
public sealed class HandshakeClient(HttpClient httpClient) {
    /// <summary>Max ping attempts before giving up. Matches Go.</summary>
    public const int MaxPingAttempts = 5;

    /// <summary>Spacing between ping attempts. Matches Go.</summary>
    public static readonly TimeSpan PingRetryDelay = TimeSpan.FromMilliseconds(300);

    private readonly HttpClient _httpClient = httpClient;

    /// <summary>
    /// Tell the old instance on <paramref name="addr"/> the new one is up. Up to
    /// <see cref="MaxPingAttempts"/> attempts at <see cref="PingRetryDelay"/> spacing
    /// (mirroring Go); true if a 200 was received.
    /// </summary>
    public async Task<bool> PingOldReadyAsync(string addr, string token, CancellationToken cancel = default) {
        var url = $"http://{addr}/ready?token={token}";
        for (var i = 0; i < MaxPingAttempts; i++) {
            try {
                using var content = new StringContent("");
                using var resp = await _httpClient.PostAsync(url, content, cancel).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode) {
                    return true;
                }
            } catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) {
                // Old instance may not be listening yet; retry below.
            }

            if (cancel.IsCancellationRequested) {
                return false;
            }
            await Task.Delay(PingRetryDelay, cancel).ConfigureAwait(false);
        }
        return false;
    }
}
