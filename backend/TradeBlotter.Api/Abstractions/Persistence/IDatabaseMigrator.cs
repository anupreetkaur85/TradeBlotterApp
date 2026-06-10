namespace TradeBlotter.Application.Abstractions.Persistence;

public interface IDatabaseMigrator
{
    void Migrate();
}
