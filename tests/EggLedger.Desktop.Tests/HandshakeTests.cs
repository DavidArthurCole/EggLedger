using EggLedger.Desktop.Update;

namespace EggLedger.Desktop.Tests;

/// <summary>
/// Loopback + token tests for the update handshake (EggLedger/update/update_handshake.go
/// + pingOldReady). A real 127.0.0.1 listener is started; the client pings it. The
/// cross-PROCESS part is manual-verify, but the token check + request/response shape
/// are exercised here, mirroring the Go TestHandshakeReadyPing case.
/// </summary>
public sealed class HandshakeTests
{
    [Fact]
    public async Task CorrectToken_SignalsServed()
    {
        const string token = "tok123";
        using var listener = HandshakeListener.Start(token);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var client = new HandshakeClient(http);

        var ok = await client.PingOldReadyAsync(listener.Address, token);

        Assert.True(ok);
        Assert.True(listener.Served.IsCompleted, "served signal should fire on a valid ping");
    }

    [Fact]
    public async Task WrongToken_DoesNotSignal()
    {
        const string token = "tok123";
        using var listener = HandshakeListener.Start(token);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var client = new HandshakeClient(http);

        var ok = await client.PingOldReadyAsync(listener.Address, "wrong");

        Assert.False(ok);
        // Give any stray handler a moment; the served task must remain incomplete.
        var won = await Task.WhenAny(listener.Served, Task.Delay(200));
        Assert.NotEqual(listener.Served, won);
        Assert.False(listener.Served.IsCompleted);
    }

    [Fact]
    public async Task ServedFiresExactlyOnce_AcrossMultiplePings()
    {
        const string token = "abc";
        using var listener = HandshakeListener.Start(token);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var client = new HandshakeClient(http);

        Assert.True(await client.PingOldReadyAsync(listener.Address, token));
        // A second valid ping still returns 200 but does not throw / double-signal.
        Assert.True(await client.PingOldReadyAsync(listener.Address, token));
        Assert.True(listener.Served.IsCompletedSuccessfully);
    }

    [Fact]
    public void Token_IsRandomHexOf32Chars()
    {
        var a = HandshakeToken.New();
        var b = HandshakeToken.New();
        Assert.Equal(32, a.Length);
        Assert.NotEqual(a, b);
        Assert.All(a, c => Assert.True(Uri.IsHexDigit(c)));
    }
}
