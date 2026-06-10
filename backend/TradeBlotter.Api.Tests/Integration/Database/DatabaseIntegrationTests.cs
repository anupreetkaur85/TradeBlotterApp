using System.Net;
using System.Net.Http.Json;
using NUnit.Framework;
using TradeBlotter.Api.IntegrationTests.Infrastructure;

namespace TradeBlotter.Api.IntegrationTests.Database;

/// <summary>
/// The thin real-SQL-Server slice: proves the stored procedures, Dapper mappings,
/// and DbUp migrations behave correctly — coverage the in-memory provider cannot
/// give. Gated by category so it is skipped where SQL Server is unavailable:
///   dotnet test --filter TestCategory=RequiresSqlServer
/// (and excluded with TestCategory!=RequiresSqlServer).
/// </summary>
[TestFixture]
[Category("RequiresSqlServer")]
public sealed class DatabaseIntegrationTests : IntegrationTestBase
{
    [Test]
    public async Task Create_then_read_roundtrips_through_stored_procedures()
    {
        HttpResponseMessage created = await Client.PostAsJsonAsync(
            "/trades", new { symbol = "aapl", side = "Buy", quantity = 100, price = 187.5m });
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        List<TradeDto>? trades = await Client.GetFromJsonAsync<List<TradeDto>>("/trades");
        Assert.That(trades, Has.Exactly(1).Matches<TradeDto>(t => t.Symbol == "AAPL" && t.Notional == 18750m));
    }

    [Test]
    public async Task Positions_weighted_average_computed_from_the_ledger()
    {
        await Client.PostAsJsonAsync("/trades", new { symbol = "AAPL", side = "Buy", quantity = 100, price = 10m });
        await Client.PostAsJsonAsync("/trades", new { symbol = "AAPL", side = "Buy", quantity = 100, price = 20m });

        List<PositionDto>? positions = await Client.GetFromJsonAsync<List<PositionDto>>("/positions");

        Assert.That(positions, Has.Count.EqualTo(1));
        Assert.That(positions![0].NetQuantity, Is.EqualTo(200));
        Assert.That(positions[0].AverageCost, Is.EqualTo(15m));
    }

    [Test]
    public async Task Seed_enable_then_disable_preserves_user_trades()
    {
        await Client.PostAsJsonAsync("/trades", new { symbol = "AAPL", side = "Buy", quantity = 5, price = 100m });

        await Client.PutAsJsonAsync("/admin/seed-data", new { enabled = true });
        SeedStatusDto? disabled = await (await Client.PutAsJsonAsync("/admin/seed-data", new { enabled = false }))
            .Content.ReadFromJsonAsync<SeedStatusDto>();
        Assert.That(disabled!.SeedRowCount, Is.EqualTo(0));

        List<TradeDto>? trades = await Client.GetFromJsonAsync<List<TradeDto>>("/trades");
        Assert.That(trades, Has.Exactly(1).Matches<TradeDto>(t => t.Symbol == "AAPL" && t.Quantity == 5));
    }

    [Test]
    public async Task Symbol_search_uses_the_sproc()
    {
        List<SymbolDto>? results = await Client.GetFromJsonAsync<List<SymbolDto>>("/symbols/search?q=ci");
        Assert.That(results, Has.Exactly(1).Matches<SymbolDto>(s => s.Symbol == "CSCO"));
    }
}

public sealed record SymbolDto(string Symbol, string Name);
