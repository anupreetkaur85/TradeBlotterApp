using TradeBlotter.Application.Abstractions.Persistence;

namespace TradeBlotter.Infrastructure.Persistence.InMemory;

/// <summary>No-op migrator for the in-memory provider (there is no schema to apply).</summary>
public sealed class NullDatabaseMigrator : IDatabaseMigrator
{
    public void Migrate()
    {
        // Intentionally empty: the in-memory store needs no migrations.
    }
}
