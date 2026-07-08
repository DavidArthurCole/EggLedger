using EggLedger.Web.Server.Sync;
using Xunit;

namespace EggLedger.Web.Server.Tests.Sync;

public class ConfigTests {
    [Fact]
    public void FromEnv_reads_authentik_settings() {
        var env = new Dictionary<string, string?> {
            ["AUTHENTIK_AUTHORITY"] = "https://auth.davidarthurcole.me/application/o/egg-ledger/",
            ["AUTHENTIK_CLIENT_ID"] = "abc",
            ["AUTHENTIK_CLIENT_SECRET"] = "secret",
        };
        var cfg = AppConfig.FromEnv(k => env.GetValueOrDefault(k));
        Assert.Equal("https://auth.davidarthurcole.me/application/o/egg-ledger/", cfg.AuthentikAuthority);
        Assert.Equal("abc", cfg.AuthentikClientId);
        Assert.Equal("secret", cfg.AuthentikClientSecret);
    }

    [Fact]
    public void FromEnv_defaults_authentik_settings_to_empty() {
        var cfg = AppConfig.FromEnv(_ => null);
        Assert.Equal("", cfg.AuthentikAuthority);
        Assert.Equal("", cfg.AuthentikClientId);
        Assert.Equal("", cfg.AuthentikClientSecret);
    }

    [Fact]
    public void FromEnv_reads_identity_api_settings() {
        var env = new Dictionary<string, string?> {
            ["IDENTITY_API_URL"] = "http://localhost:8090",
            ["IDENTITY_API_SECRET"] = "shh",
        };
        var cfg = AppConfig.FromEnv(k => env.GetValueOrDefault(k));
        Assert.Equal("http://localhost:8090", cfg.IdentityApiUrl);
        Assert.Equal("shh", cfg.IdentityApiSecret);
    }

    [Fact]
    public void FromEnv_defaults_identity_api_settings_to_empty() {
        var cfg = AppConfig.FromEnv(_ => null);
        Assert.Equal("", cfg.IdentityApiUrl);
        Assert.Equal("", cfg.IdentityApiSecret);
    }
}
