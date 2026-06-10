using TradeBlotter.Application.Contracts.Responses;
using TradeBlotter.Application.Contracts.Results;
using TradeBlotter.Application.Models;

namespace TradeBlotter.Application.Abstractions.Services;

public interface ITradeService
{
    Task<CreateTradeResult> CreateAsync(
        NormalizedTrade trade,
        Guid? clientRequestId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TradeResponse>> GetTradesAsync(
        CancellationToken cancellationToken);
}
