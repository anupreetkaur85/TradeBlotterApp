using System.Reflection;
using DbUp;
using DbUp.Engine;
using TradeBlotter.Application.Abstractions.Persistence;

namespace TradeBlotter.Infrastructure.Persistence.Migrations;

/// <summary>
/// Runs the embedded, ordered SQL migration scripts with DbUp. Idempotent: DbUp
/// journals applied scripts, and the scripts themselves guard with IF NOT EXISTS.
/// </summary>
public sealed class DatabaseMigrator : IDatabaseMigrator
{
    private const string ScriptPrefix = "TradeBlotter.Infrastructure.Migrations.";
    private readonly string _connectionString;

    public DatabaseMigrator(string connectionString) => _connectionString = connectionString;

    public void Migrate()
    {
        EnsureDatabase.For.SqlDatabase(_connectionString);

        UpgradeEngine upgrader = DeployChanges.To
            .SqlDatabase(_connectionString)
            .WithScriptsEmbeddedInAssembly(
                Assembly.GetExecutingAssembly(),
                name => name.StartsWith(ScriptPrefix, StringComparison.Ordinal))
            .WithTransactionPerScript()
            .LogToConsole()
            .Build();

        DatabaseUpgradeResult result = upgrader.PerformUpgrade();
        if (!result.Successful)
        {
            throw new InvalidOperationException("Database migration failed.", result.Error);
        }
    }
}
