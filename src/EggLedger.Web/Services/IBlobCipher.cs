namespace EggLedger.Web.Services;

public interface IBlobCipher {
    ValueTask<string> EncryptAsync(string hexKey, byte[] plaintext, CancellationToken ct = default);
    ValueTask<byte[]> DecryptAsync(string hexKey, string b64, CancellationToken ct = default);
}
