using EggLedger.Domain.Crypto;

namespace EggLedger.Web.Services;

/// <summary>
/// In-process AES-256-GCM via the managed <see cref="BlobCrypto"/>. Used by the
/// desktop host, where System.Security.Cryptography.AesGcm is available.
/// </summary>
public sealed class LocalBlobCipher : IBlobCipher
{
    public ValueTask<string> EncryptAsync(string hexKey, byte[] plaintext, CancellationToken ct = default) =>
        ValueTask.FromResult(BlobCrypto.Encrypt(hexKey, plaintext));

    public ValueTask<byte[]> DecryptAsync(string hexKey, string b64, CancellationToken ct = default) =>
        ValueTask.FromResult(BlobCrypto.Decrypt(hexKey, b64));
}
