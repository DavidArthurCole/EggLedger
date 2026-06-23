using System.Net;

namespace EggLedger.Desktop.Update;

/// <summary>
/// Localhost-only HTTP listener owned by the OLD instance during a self-update.
/// Ports EggLedger/update/update_handshake.go startHandshakeListener. A POST to
/// /ready carrying the matching token signals <see cref="Served"/> exactly once
/// (CompareAndSwap-gated like the Go atomic flag); a wrong/missing token is
/// rejected with 403 and never signals. The new instance proves identity over
/// loopback before the old instance exits.
///
/// The token check + the request/response shape are unit-testable against a real
/// 127.0.0.1 listener (see HandshakeTests). The cross-PROCESS part is manual-verify.
/// </summary>
public sealed class HandshakeListener : IDisposable
{
    private readonly HttpListener _listener;
    private readonly string _token;
    private readonly TaskCompletionSource _served =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _handoffDone;
    private CancellationTokenSource? _cts;

    private HandshakeListener(HttpListener listener, string token, string address)
    {
        _listener = listener;
        _token = token;
        Address = address;
    }

    /// <summary>The bound address ("127.0.0.1:N") to hand to the new process.</summary>
    public string Address { get; }

    /// <summary>Completes once a valid /ready ping has arrived (the handshake is served).</summary>
    public Task Served => _served.Task;

    /// <summary>
    /// Open a loopback HTTP listener on an ephemeral port and begin serving /ready.
    /// Ports startHandshakeListener; binds 127.0.0.1 only.
    /// </summary>
    public static HandshakeListener Start(string token)
    {
        // HttpListener needs an explicit port; probe a free loopback port first.
        var port = FindFreeLoopbackPort();
        var listener = new HttpListener();
        var address = $"127.0.0.1:{port}";
        listener.Prefixes.Add($"http://{address}/");
        listener.Start();

        var self = new HandshakeListener(listener, token, address);
        self._cts = new CancellationTokenSource();
        _ = self.ServeLoopAsync(self._cts.Token);
        return self;
    }

    private static int FindFreeLoopbackPort()
    {
        var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private async Task ServeLoopAsync(CancellationToken cancel)
    {
        while (!cancel.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            HandleRequest(ctx);
        }
    }

    private void HandleRequest(HttpListenerContext ctx)
    {
        var response = ctx.Response;
        try
        {
            var isReady = string.Equals(ctx.Request.Url?.AbsolutePath, "/ready", StringComparison.Ordinal);
            var token = ctx.Request.QueryString["token"];

            if (!isReady)
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }
            if (!string.Equals(token, _token, StringComparison.Ordinal))
            {
                response.StatusCode = (int)HttpStatusCode.Forbidden;
                return;
            }

            response.StatusCode = (int)HttpStatusCode.OK;
            // Gate the one-shot signal so concurrent /ready requests are race-free.
            if (Interlocked.CompareExchange(ref _handoffDone, 1, 0) == 0)
            {
                _served.TrySetResult();
            }
        }
        finally
        {
            try
            {
                response.Close();
            }
            catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
            {
            }
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        if (_listener.IsListening)
        {
            _listener.Stop();
        }
        _listener.Close();
    }
}
