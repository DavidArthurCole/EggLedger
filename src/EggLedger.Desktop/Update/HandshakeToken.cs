using System.Security.Cryptography;

namespace EggLedger.Desktop.Update;

public static class HandshakeToken {
    public static string New() {
        try {
            var b = RandomNumberGenerator.GetBytes(16);
            return Convert.ToHexStringLower(b);
        } catch (CryptographicException) {
            return "egg-update";
        }
    }
}
