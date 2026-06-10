namespace TradeBlotter.Api.IntegrationTests.Infrastructure;

public static class TestDatabase
{
    /// <summary>
    /// LocalDB test database. Override with TRADEBLOTTER_TEST_CONNECTION to point at
    /// a different instance. The database is created by the app's DbUp migrations on
    /// first startup.
    /// </summary>
    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("TRADEBLOTTER_TEST_CONNECTION")
        ?? "Server=(localdb)\\MSSQLLocalDB;Database=TradeBlotter_Test;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";
}
