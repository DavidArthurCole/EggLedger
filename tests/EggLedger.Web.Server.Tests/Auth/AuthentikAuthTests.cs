using EggLedger.Web.Server.Auth;
using EggLedger.Web.Server.Sync;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace EggLedger.Web.Server.Tests.Auth;

public class AuthentikAuthTests {
    private static AppConfig ConfigWith(string authority, string clientId = "", string clientSecret = "") => new(
        ListenAddr: "", DatabaseUrl: "", DiscordClientId: "", DiscordClientSecret: "", RedirectUrl: "",
        BotToken: "", GuildId: "", SharedRoleId: "", DeployAgentUrl: "", DeployAgentSecret: "", MennoFunctionKey: "",
        AdminUserIds: new HashSet<string>(),
        AuthentikAuthority: authority, AuthentikClientId: clientId, AuthentikClientSecret: clientSecret,
        TrustedProxyNetworks: [],
        BuildSha: "dev", BuildDate: "dev", DataProtectionCertPath: "", DataProtectionCertPassword: "");

    // Npgsql connections are lazy: constructing a data source against this connection string
    // never actually dials Postgres. Safe here because these tests only check scheme
    // registration, never a real OIDC round-trip that would invoke the resolver.
    private static AuthentikIdentityResolver UnusedResolver() =>
        new(NpgsqlDataSource.Create("Host=localhost;Database=doesnotmatter"));

    [Fact]
    public async Task AddIfConfigured_registers_oidc_scheme_when_authority_configured() {
        var services = new ServiceCollection();
        var builder = services.AddAuthentication(AuthScheme.Cookie).AddCookie(AuthScheme.Cookie);
        var registered = AuthentikAuth.AddIfConfigured(builder, ConfigWith(
            "https://auth.davidarthurcole.me/application/o/egg-ledger/", "abc", "secret"), UnusedResolver());
        Assert.True(registered);

        var provider = services.BuildServiceProvider();
        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var scheme = await schemeProvider.GetSchemeAsync(OpenIdConnectDefaults.AuthenticationScheme);
        Assert.NotNull(scheme);
    }

    [Fact]
    public async Task AddIfConfigured_noop_when_authority_empty() {
        var services = new ServiceCollection();
        var builder = services.AddAuthentication(AuthScheme.Cookie).AddCookie(AuthScheme.Cookie);
        var registered = AuthentikAuth.AddIfConfigured(builder, ConfigWith(""), UnusedResolver());
        Assert.False(registered);

        var provider = services.BuildServiceProvider();
        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var scheme = await schemeProvider.GetSchemeAsync(OpenIdConnectDefaults.AuthenticationScheme);
        Assert.Null(scheme);
    }
}
