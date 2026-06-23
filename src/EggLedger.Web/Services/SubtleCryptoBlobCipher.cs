using Microsoft.JSInterop;

namespace EggLedger.Web.Services;

/// <summary>
/// AES-256-GCM via the browser SubtleCrypto API (JS interop). Used by the WASM
/// host because System.Security.Cryptography.AesGcm is not supported on the
/// browser runtime. Wire format is identical to <see cref="LocalBlobCipher"/>
/// and the Go server: base64(nonce || ciphertext || tag). Plaintext crosses the
/// JS boundary as base64 to avoid byte[] marshalling quirks.
/// </summary>
public sealed class SubtleCryptoBlobCipher : IBlobCipher
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;

    public SubtleCryptoBlobCipher(IJSRuntime js) => _js = js;

    private async ValueTask<IJSObjectReference> ModuleAsync() =>
        _module ??= await _js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/EggLedger.Web/js/blobcrypto.js");

    public async ValueTask<string> EncryptAsync(string hexKey, byte[] plaintext, CancellationToken ct = default)
    {
        var module = await ModuleAsync();
        string plaintextB64 = Convert.ToBase64String(plaintext);
        return await module.InvokeAsync<string>("encrypt", ct, hexKey, plaintextB64);
    }

    public async ValueTask<byte[]> DecryptAsync(string hexKey, string b64, CancellationToken ct = default)
    {
        var module = await ModuleAsync();
        string plaintextB64 = await module.InvokeAsync<string>("decrypt", ct, hexKey, b64);
        return Convert.FromBase64String(plaintextB64);
    }
}
