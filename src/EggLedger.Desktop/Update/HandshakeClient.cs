namespace EggLedger.Desktop.Update;

/// <summary>
/// New instance's side of the handshake: POST /ready?token to the old instance,
/// best-effort with a few quick retries until a 200.
/// </summary>
public sealed class HandshakeClient(HttpClient httpClient) {
    private readonly HttpClient _httpClient = httpClient;

    /// <summary>
    /// Tell the old instance on <paramref name="addr"/> the new one is up. Up to 5
    /// attempts at 300ms spacing (mirroring Go); true if a 200 was received.
    /// </summary>
    public async Task<bool> PingOldReadyAsync(string addr, string token, CancellationToken cancel = default) {
        var url = $"http://{addr}/ready?token={token}";
        for (var i = 0; i < 5; i++) {
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
            await Task.Delay(TimeSpan.FromMilliseconds(300), cancel).ConfigureAwait(false);
        }
        return false;
    }
}
