namespace EggLedger.Web.Services;

/// <summary>
/// AES-256-GCM blob encryption seam. The wire format (base64 of
/// nonce || ciphertext || tag) is fixed by the Go server, so every
/// implementation must stay byte-compatible. The desktop host uses the managed
/// <see cref="EggLedger.Domain.Crypto.BlobCrypto"/>; the browser host cannot
/// (System.Security.Cryptography.AesGcm is unsupported on the WASM runtime) and
/// uses the browser SubtleCrypto API via JS interop instead.
/// </summary>
public interface IBlobCipher
{
    ValueTask<string> EncryptAsync(string hexKey, byte[] plaintext, CancellationToken ct = default);

    ValueTask<byte[]> DecryptAsync(string hexKey, string b64, CancellationToken ct = default);
}
