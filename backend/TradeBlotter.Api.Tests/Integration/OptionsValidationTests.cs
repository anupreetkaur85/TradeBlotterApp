using NUnit.Framework;
using TradeBlotter.Api.IntegrationTests.Infrastructure;

namespace TradeBlotter.Api.IntegrationTests;

[TestFixture]
public sealed class OptionsValidationTests
{
    [Test]
    public void Startup_fails_when_seed_on_startup_is_requested_without_enable()
    {
        using var factory = new InMemoryApiFactory(new Dictionary<string, string?>
        {
            ["Seeding:Enabled"] = "false",
            ["Seeding:RunOnStartup"] = "true", // invalid combination -> ValidateOnStart should fail
        });

        // CreateClient builds and starts the host, triggering options validation.
        Assert.That(() => factory.CreateClient(), Throws.Exception);
    }
}
