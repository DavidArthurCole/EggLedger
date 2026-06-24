namespace EggLedger.Web.Services;

/// <summary>AES-256-GCM blob encryption seam. The wire format (base64 of nonce || ciphertext || tag) is fixed by the server, so every implementation must stay byte-compatible. Desktop uses managed AesGcm; the WASM host uses SubtleCrypto since AesGcm is unsupported there.</summary>
public interface IBlobCipher {
    ValueTask<string> EncryptAsync(string hexKey, byte[] plaintext, CancellationToken ct = default);

    ValueTask<byte[]> DecryptAsync(string hexKey, string b64, CancellationToken ct = default);
}
