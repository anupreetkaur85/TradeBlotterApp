using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace TradeBlotter.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real Minimal API against a configurable connection string. Migrations
/// run on startup so the target database (and its sprocs) is created automatically.
/// </summary>
public sealed class IntegrationTestFactory : WebApplicationFactory<Program>
{
    private readonly IDictionary<string, string?> _overrides;

    public IntegrationTestFactory(string connectionString, IDictionary<string, string?>? overrides = null)
    {
        // Program reads the connection string BEFORE builder.Build(), so an
        // in-memory config override arrives too late. An environment variable is
        // present on the default config sources from the start, so it takes effect.
        Environment.SetEnvironmentVariable("ConnectionStrings__TradeBlotter", connectionString);
        Environment.SetEnvironmentVariable("Database__UseInMemory", "false"); // force the SQL provider

        _overrides = new Dictionary<string, string?>
        {
            ["ConnectionStrings:TradeBlotter"] = connectionString,
            ["Database:UseInMemory"] = "false",
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
