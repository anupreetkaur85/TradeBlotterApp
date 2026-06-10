using Microsoft.Data.SqlClient;
using NUnit.Framework;
using Respawn;
using Respawn.Graph;

namespace TradeBlotter.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Base fixture: starts the API once per fixture (creating/migrating the test
/// database) and resets the Trades table before each test with Respawn, leaving
/// the DbUp journal and sprocs intact.
/// </summary>
public abstract class IntegrationTestBase
{
    private IntegrationTestFactory _factory = null!;
    private Respawner _respawner = null!;
    private string _connectionString = null!;

    protected HttpClient Client { get; private set; } = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUpAsync()
    {
        _connectionString = TestDatabase.ConnectionString;
        _factory = new IntegrationTestFactory(_connectionString);

        // Triggers host startup -> DbUp migrations create the database, tables, and sprocs.
        Client = _factory.CreateClient();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            TablesToInclude = [new Table("dbo", "Trades")],
            WithReseed = true,
        });
    }

    [SetUp]
    public async Task ResetDatabaseAsync()
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        Client?.Dispose();
        _factory?.Dispose();
    }
}
