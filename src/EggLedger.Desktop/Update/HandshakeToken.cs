using System.Security.Cryptography;

namespace EggLedger.Desktop.Update;

/// <summary>Random hex token used to authenticate the update handshake ping. Ports newHandshakeToken.</summary>
public static class HandshakeToken
{
    /// <summary>16 random bytes, hex-encoded (lowercase). Falls back to "egg-update" only if RNG fails.</summary>
    public static string New()
    {
        try
        {
            var b = RandomNumberGenerator.GetBytes(16);
            return Convert.ToHexStringLower(b);
        }
        catch (CryptographicException)
        {
            return "egg-update";
        }
    }
}
