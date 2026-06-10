using NUnit.Framework;

namespace TradeBlotter.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Base for fast component tests: a fresh in-memory API per test (so each test is
/// isolated without a database or Respawn). No SQL Server required.
/// </summary>
public abstract class ApiTestBase
{
    private InMemoryApiFactory _factory = null!;

    protected HttpClient Client { get; private set; } = null!;

    [SetUp]
    public void ApiSetUp()
    {
        _factory = new InMemoryApiFactory();
        Client = _factory.CreateClient();
    }

    [TearDown]
    public void ApiTearDown()
    {
        Client?.Dispose();
        _factory?.Dispose();
    }
}
