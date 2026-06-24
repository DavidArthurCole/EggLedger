using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EggLedger.Domain.Crypto;

namespace EggLedger.Domain.Tests.Crypto;

/// <summary>
/// Cross-decrypt acceptance for the AES-256-GCM blob contract (Go
/// cloudsync.encryptBlob/decryptBlob). The golden blob was produced by a Go
/// program using the same algorithm as blob.go with a pinned 12-byte nonce; see
/// Fixtures/crypto/go-blob-fixture.json (generated via aesgen scratch dir).
/// </summary>
public class BlobCryptoTests {
    private sealed record Fixture(
        string Key,
        string Plaintext,
        string NonceHex,
        string Blob,
        string PlaintextBase64);

    private static Fixture LoadFixture() {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "crypto", "go-blob-fixture.json");
        var json = File.ReadAllText(path);
        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        return JsonSerializer.Deserialize<Fixture>(json, opts)
            ?? throw new InvalidOperationException("fixture deserialized to null");
    }

    // Mandatory: C# decrypts a Go-produced blob to the original plaintext.
    [Fact]
    public void Decrypt_GoProducedBlob_RecoversPlaintext() {
        var fx = LoadFixture();
        var plaintext = BlobCrypto.Decrypt(fx.Key, fx.Blob);
        Assert.Equal(fx.Plaintext, Encoding.UTF8.GetString(plaintext));
        Assert.Equal(Convert.FromBase64String(fx.PlaintextBase64), plaintext);
    }

    // C#-encrypt -> C#-decrypt round-trip.
    [Fact]
    public void EncryptDecrypt_RoundTrips() {
        var fx = LoadFixture();
        var pt = Encoding.UTF8.GetBytes("the quick brown chicken lays 9.99e21 eggs");
        var blob = BlobCrypto.Encrypt(fx.Key, pt);
        var back = BlobCrypto.Decrypt(fx.Key, blob);
        Assert.Equal(pt, back);
    }

    // Empty plaintext round-trips (12-byte nonce + 16-byte tag only).
    [Fact]
    public void EncryptDecrypt_EmptyPlaintext_RoundTrips() {
        var fx = LoadFixture();
        var blob = BlobCrypto.Encrypt(fx.Key, []);
        Assert.Empty(BlobCrypto.Decrypt(fx.Key, blob));
    }

    // Each encrypt uses a fresh random nonce, so ciphertext differs run to run.
    [Fact]
    public void Encrypt_UsesRandomNonce() {
        var fx = LoadFixture();
        var pt = Encoding.UTF8.GetBytes("same input");
        Assert.NotEqual(BlobCrypto.Encrypt(fx.Key, pt), BlobCrypto.Encrypt(fx.Key, pt));
    }

    // Tampered ciphertext fails the GCM auth tag.
    [Fact]
    public void Decrypt_TamperedBlob_Throws() {
        var fx = LoadFixture();
        var data = Convert.FromBase64String(fx.Blob);
        data[^1] ^= 0xFF;
        var tampered = Convert.ToBase64String(data);
        Assert.ThrowsAny<CryptographicException>(() => BlobCrypto.Decrypt(fx.Key, tampered));
    }

    [Fact]
    public void Decrypt_WrongKey_Throws() {
        var fx = LoadFixture();
        var wrong = "ff" + fx.Key[2..];
        Assert.ThrowsAny<CryptographicException>(() => BlobCrypto.Decrypt(wrong, fx.Blob));
    }

    [Theory]
    [InlineData("00")]
    [InlineData("000102030405060708090a0b0c0d0e0f")]
    public void Decrypt_BadKeyLength_Throws(string shortKey) {
        var fx = LoadFixture();
        Assert.Throws<CryptographicException>(() => BlobCrypto.Decrypt(shortKey, fx.Blob));
    }
}
