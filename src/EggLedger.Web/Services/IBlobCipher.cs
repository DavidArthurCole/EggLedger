namespace EggLedger.Web.Services;

/// <summary>AES-256-GCM blob encryption seam, backed by managed AesGcm. The wire format (base64 of nonce || ciphertext || tag) is server-fixed, so any implementation must stay byte-compatible.</summary>
public interface IBlobCipher {
    ValueTask<string> EncryptAsync(string hexKey, byte[] plaintext, CancellationToken ct = default);
    ValueTask<byte[]> DecryptAsync(string hexKey, string b64, CancellationToken ct = default);
}
