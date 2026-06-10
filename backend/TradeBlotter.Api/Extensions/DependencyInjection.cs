using Microsoft.Extensions.DependencyInjection;
using Polly;
using TradeBlotter.Application.Abstractions.Persistence;
using TradeBlotter.Application.Abstractions.Services;
using TradeBlotter.Application.Services;
using TradeBlotter.Infrastructure.Persistence.Connections;
using TradeBlotter.Infrastructure.Persistence.InMemory;
using TradeBlotter.Infrastructure.Persistence.Migrations;
using TradeBlotter.Infrastructure.Persistence.Repositories;
using TradeBlotter.Infrastructure.Persistence.Resilience;

namespace TradeBlotter.Infrastructure;

public static class DependencyInjection
{
    /// <summary>SQL Server persistence (Dapper + stored procedures + DbUp migrations).</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<ISqlConnectionFactory>(_ => new SqlConnectionFactory(connectionString));
        services.AddSingleton<IDatabaseMigrator>(_ => new DatabaseMigrator(connectionString));
        services.AddSingleton<ResiliencePipeline>(_ => DatabaseResilience.CreatePipeline());
        services.AddSingleton<ITradeRepository, TradeRepository>();
        services.AddSingleton<ISymbolRepository, SymbolRepository>();
        return services;
    }

    /// <summary>In-memory persistence — no SQL Server required (reviewers / fast tests).</summary>
    public static IServiceCollection AddInMemoryPersistence(this IServiceCollection services)
    {
        services.AddSingleton<IDatabaseMigrator, NullDatabaseMigrator>();
        services.AddSingleton<ITradeRepository, InMemoryTradeRepository>();
        services.AddSingleton<ISymbolRepository, InMemorySymbolRepository>();
        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ITradeService, TradeService>();
        services.AddScoped<IPositionService, PositionService>();
        services.AddScoped<ISeedService, SeedService>();
        return services;
    }
}
