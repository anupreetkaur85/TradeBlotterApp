using TradeBlotter.Application.Abstractions.Persistence;
using TradeBlotter.Application.Abstractions.Services;
using TradeBlotter.Application.Contracts.Responses;
using TradeBlotter.Domain;

namespace TradeBlotter.Application.Services;

/// <summary>
/// Computes positions by folding the trade ledger. Positions are never stored.
/// Assessment-scale correctness is favored over cache invalidation complexity.
/// </summary>
public sealed class PositionService : IPositionService
{
    private readonly ITradeRepository _repository;
    public PositionService(ITradeRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<PositionResponse>> GetPositionsAsync(CancellationToken ct)
    {
        IReadOnlyList<PositionTrade> trades = await _repository.GetForPositionsAsync(ct);
        IReadOnlyList<Position> positions = PositionCalculator.Calculate(trades);

        var mapped = new List<PositionResponse>(positions.Count);
        for (int i = 0; i < positions.Count; i++)
        {
            Position p = positions[i];
            mapped.Add(new PositionResponse(p.Symbol, p.NetQuantity, decimal.Round(p.AverageCost, 4)));
        }

        return mapped;
    }
}
