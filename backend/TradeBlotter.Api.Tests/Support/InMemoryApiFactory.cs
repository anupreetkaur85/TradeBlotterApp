using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace TradeBlotter.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real Minimal API against the in-memory persistence provider — no SQL
/// Server required. Used by the fast component tests (the bulk of the suite).
/// </summary>
public sealed class InMemoryApiFactory : WebApplicationFactory<Program>
{
    private readonly IDictionary<string, string?> _overrides;

    public InMemoryApiFactory(IDictionary<string, string?>? overrides = null)
    {
        // Program reads Database:UseInMemory BEFORE builder.Build(), so it must be an
        // environment variable to take effect (in-memory config arrives too late).
        Environment.SetEnvironmentVariable("Database__UseInMemory", "true");

        _overrides = new Dictionary<string, string?>
        {
            ["Database:UseInMemory"] = "true",
            ["Migrations:RunOnStartup"] = "true",
            ["Admin:Enabled"] = "true",
            ["Seeding:Enabled"] = "true",
            ["Seeding:RunOnStartup"] = "false",
        };

        if (overrides is not null)
        {
            foreach (KeyValuePair<string, string?> pair in overrides)
            {
                _overrides[pair.Key] = pair.Value;
            }
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(_overrides));
    }
}
