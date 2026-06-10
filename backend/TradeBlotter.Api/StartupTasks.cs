using System.Diagnostics;
using Microsoft.Extensions.Options;
using TradeBlotter.Application.Abstractions.Persistence;
using TradeBlotter.Application.Abstractions.Services;
using TradeBlotter.Application.Options;

namespace TradeBlotter.Api;

public static class StartupTasks
{
    public static async Task RunAsync(WebApplication app)
    {
        ILogger logger = app.Logger;
        CancellationToken ct = app.Lifetime.ApplicationStopping;

        MigrationOptions migration = app.Services.GetRequiredService<IOptions<MigrationOptions>>().Value;
        SeedingOptions seeding = app.Services.GetRequiredService<IOptions<SeedingOptions>>().Value;
        AdminOptions admin = app.Services.GetRequiredService<IOptions<AdminOptions>>().Value;

        logger.LogInformation(
            "Database startup configuration: migrate={MigrateOnStartup}, seedEnabled={SeedEnabled}, seedOnStartup={SeedOnStartup}, adminEnabled={AdminEnabled}, environment={Environment}",
            migration.RunOnStartup, seeding.Enabled, seeding.RunOnStartup, admin.Enabled, app.Environment.EnvironmentName);

        if (migration.RunOnStartup)
        {
            var stopwatch = Stopwatch.StartNew();
            logger.LogInformation("Database migration starting");
            try
            {
                app.Services.GetRequiredService<IDatabaseMigrator>().Migrate();
            }
            catch (Exception exception)
            {
                // Serving traffic against an unknown schema is unsafe: fail startup.
                logger.LogCritical(exception, "Database migration failed after {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
                throw;
            }

            logger.LogInformation("Database migration completed in {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
        }

        if (seeding is { Enabled: true, RunOnStartup: true })
        {
            logger.LogInformation("Startup seed enable requested");
            using IServiceScope scope = app.Services.CreateScope();
            ISeedService seedService = scope.ServiceProvider.GetRequiredService<ISeedService>();

            // The configuration explicitly asked for a database state; if it cannot
            // be achieved, fail startup rather than serve an unexpected dataset.
            await seedService.SetEnabledAsync(true, ct);
        }
        else
        {
            string reason = !seeding.Enabled ? "SeedingDisabled" : "RunOnStartupDisabled";
            logger.LogInformation("Startup seed skipped: reason={Reason}", reason);
        }
    }
}
