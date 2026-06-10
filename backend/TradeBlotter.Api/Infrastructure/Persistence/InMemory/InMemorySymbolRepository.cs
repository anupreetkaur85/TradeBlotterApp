using TradeBlotter.Application.Abstractions.Persistence;
using TradeBlotter.Application.Contracts.Responses;

namespace TradeBlotter.Infrastructure.Persistence.InMemory;

/// <summary>In-memory symbol search over the bundled <see cref="SymbolCatalog"/>.</summary>
public sealed class InMemorySymbolRepository : ISymbolRepository
{
    public Task<IReadOnlyList<SymbolResponse>> SearchAsync(string query, int limit, CancellationToken ct) =>
        Task.FromResult(SymbolCatalog.Search(query, limit));
}
