using System.Net;
using System.Net.Http.Json;
using NUnit.Framework;
using TradeBlotter.Api.IntegrationTests.Infrastructure;

namespace TradeBlotter.Api.IntegrationTests;

[TestFixture]
public sealed class SeedAdminTests : ApiTestBase
{
    [Test]
    public async Task Enable_then_disable_seed_preserves_user_trades()
    {
        // A user trade that must survive a seed disable.
        await Client.PostAsJsonAsync("/trades", new { symbol = "AAPL", side = "Buy", quantity = 5, price = 100m });

        HttpResponseMessage enable = await Client.PutAsJsonAsync("/admin/seed-data", new { enabled = true });
        SeedStatusDto? enabled = await enable.Content.ReadFromJsonAsync<SeedStatusDto>();
        Assert.That(enabled!.Enabled, Is.True);
        Assert.That(enabled.SeedRowCount, Is.GreaterThan(0));

        HttpResponseMessage disable = await Client.PutAsJsonAsync("/admin/seed-data", new { enabled = false });
        SeedStatusDto? disabled = await disable.Content.ReadFromJsonAsync<SeedStatusDto>();
        Assert.That(disabled!.SeedRowCount, Is.EqualTo(0));

        // The user trade is still present after the seed rows were removed.
        List<TradeDto>? trades = await Client.GetFromJsonAsync<List<TradeDto>>("/trades");
        Assert.That(trades, Has.Exactly(1).Matches<TradeDto>(
            t => t.Symbol == "AAPL" && t.Quantity == 5));
    }

    [Test]
    public async Task Enable_seed_is_idempotent()
    {
        await Client.PutAsJsonAsync("/admin/seed-data", new { enabled = true });
        SeedStatusDto? first = await (await Client.GetAsync("/admin/seed-data")).Content.ReadFromJsonAsync<SeedStatusDto>();

        await Client.PutAsJsonAsync("/admin/seed-data", new { enabled = true });
        SeedStatusDto? second = await (await Client.GetAsync("/admin/seed-data")).Content.ReadFromJsonAsync<SeedStatusDto>();

        Assert.That(second!.SeedRowCount, Is.EqualTo(first!.SeedRowCount));
    }

    [Test]
    public async Task Disabling_seed_immediately_removes_seed_positions()
    {
        await Client.PutAsJsonAsync("/admin/seed-data", new { enabled = true });
        List<PositionDto>? before =
            await Client.GetFromJsonAsync<List<PositionDto>>("/positions");
        Assert.That(before, Is.Not.Empty);

        await Client.PutAsJsonAsync("/admin/seed-data", new { enabled = false });
        List<PositionDto>? after =
            await Client.GetFromJsonAsync<List<PositionDto>>("/positions");

        Assert.That(after, Is.Empty);
    }
}

/// <summary>
/// Admin routes mapped (Admin:Enabled) but seed mutation forbidden (Seeding:Enabled=false):
/// PUT must return 409 Conflict.
/// </summary>
[TestFixture]
public sealed class SeedDisabledTests
{
    private InMemoryApiFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        _factory = new InMemoryApiFactory(new Dictionary<string, string?>
        {
            ["Seeding:Enabled"] = "false",
            ["Admin:Enabled"] = "true",
            ["Seeding:RunOnStartup"] = "false",
        });
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task Status_is_readable_but_mutation_is_conflict()
    {
        HttpResponseMessage status = await _client.GetAsync("/admin/seed-data");
        Assert.That(status.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        HttpResponseMessage mutate = await _client.PutAsJsonAsync("/admin/seed-data", new { enabled = true });
        Assert.That(mutate.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }
}
