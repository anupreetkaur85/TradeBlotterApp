using TradeBlotter.Application.Contracts.Responses;

namespace TradeBlotter.Application.Abstractions.Persistence;

/// <summary>Read-only search over the reference symbol universe (autocomplete).</summary>
public interface ISymbolRepository
{
    Task<IReadOnlyList<SymbolResponse>> SearchAsync(string query, int limit, CancellationToken ct);
}
