using System.Net;
using System.Net.Http.Json;
using NUnit.Framework;
using TradeBlotter.Api.IntegrationTests.Infrastructure;

namespace TradeBlotter.Api.IntegrationTests;

[TestFixture]
public sealed class TradeApiTests : ApiTestBase
{
    private static HttpRequestMessage PostTrade(object body, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/trades")
        {
            Content = JsonContent.Create(body),
        };
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return request;
    }

    [Test]
    public async Task Health_returns_ok()
    {
        HttpResponseMessage live = await Client.GetAsync("/health/live");
        HttpResponseMessage ready = await Client.GetAsync("/health/ready");

        Assert.That(live.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(ready.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Create_returns_201_with_computed_notional()
    {
        HttpResponseMessage response = await Client.SendAsync(
            PostTrade(new { symbol = "aapl", side = "Buy", quantity = 100, price = 187.5m }));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        TradeDto? trade = await response.Content.ReadFromJsonAsync<TradeDto>();
        Assert.That(trade, Is.Not.Null);
        Assert.That(trade!.Symbol, Is.EqualTo("AAPL"));      // normalized
        Assert.That(trade.Side, Is.EqualTo("Buy"));
        Assert.That(trade.Notional, Is.EqualTo(18750m));     // 100 * 187.5
        Assert.That(trade.TradeId, Is.GreaterThan(0));
        Assert.That(trade.Timestamp, Is.Not.EqualTo(default(DateTimeOffset)));
    }

    [Test]
    public async Task Invalid_trade_returns_validation_problem()
    {
        HttpResponseMessage response = await Client.SendAsync(
            PostTrade(new { symbol = "", side = "Buy", quantity = 0, price = -1 }));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Trades_are_returned_newest_first()
    {
        await Client.SendAsync(PostTrade(new { symbol = "AAPL", side = "Buy", quantity = 10, price = 100m }));
        await Client.SendAsync(PostTrade(new { symbol = "MSFT", side = "Buy", quantity = 20, price = 200m }));

        List<TradeDto>? trades = await Client.GetFromJsonAsync<List<TradeDto>>("/trades");

        Assert.That(trades, Is.Not.Null);
        Assert.That(trades!, Has.Count.EqualTo(2));
        Assert.That(trades[0].Symbol, Is.EqualTo("MSFT")); // newest first
    }

    [Test]
    public async Task Positions_reflect_weighted_average_and_omit_flat()
    {
        // AAPL: 100@10 then 100@20 -> 200 @ 15. MSFT: buy then full sell -> flat (omitted).
        await Client.SendAsync(PostTrade(new { symbol = "AAPL", side = "Buy", quantity = 100, price = 10m }));
        await Client.SendAsync(PostTrade(new { symbol = "AAPL", side = "Buy", quantity = 100, price = 20m }));
        await Client.SendAsync(PostTrade(new { symbol = "MSFT", side = "Buy", quantity = 50, price = 300m }));
        await Client.SendAsync(PostTrade(new { symbol = "MSFT", side = "Sell", quantity = 50, price = 310m }));

        List<PositionDto>? positions = await Client.GetFromJsonAsync<List<PositionDto>>("/positions");

        Assert.That(positions, Is.Not.Null);
        Assert.That(positions!, Has.Count.EqualTo(1));
        Assert.That(positions[0].Symbol, Is.EqualTo("AAPL"));
        Assert.That(positions[0].NetQuantity, Is.EqualTo(200));
        Assert.That(positions[0].AverageCost, Is.EqualTo(15m));
    }

    [Test]
    public async Task Duplicate_idempotency_key_inserts_one_row_and_replays()
    {
        string key = "11111111-1111-1111-1111-111111111111";
        var body = new { symbol = "AAPL", side = "Buy", quantity = 100, price = 187.5m };

        HttpResponseMessage first = await Client.SendAsync(PostTrade(body, key));
        HttpResponseMessage second = await Client.SendAsync(PostTrade(body, key));

        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK)); // replay

        TradeDto? firstTrade = await first.Content.ReadFromJsonAsync<TradeDto>();
        TradeDto? secondTrade = await second.Content.ReadFromJsonAsync<TradeDto>();
        Assert.That(secondTrade!.TradeId, Is.EqualTo(firstTrade!.TradeId));

        List<TradeDto>? trades = await Client.GetFromJsonAsync<List<TradeDto>>("/trades");
        Assert.That(trades, Has.Count.EqualTo(1)); // exactly one row
    }

    [Test]
    public async Task Malformed_idempotency_key_returns_validation_problem()
    {
        HttpResponseMessage response = await Client.SendAsync(
            PostTrade(
                new { symbol = "AAPL", side = "Buy", quantity = 10, price = 100m },
                "not-a-guid"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}
