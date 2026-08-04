namespace EggLedger.Web.Server.Tests.Sync.Auth;

public static class TestDbUrl {
    public static string? Value => Environment.GetEnvironmentVariable("EGGLEDGER_TEST_DB_URL");

    public static void SkipIfNotConfigured(string context) =>
        Skip.If(string.IsNullOrEmpty(Value), $"EGGLEDGER_TEST_DB_URL not set; live Postgres {context} test skipped.");
}
