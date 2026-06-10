using TradeBlotter.Application.Contracts.Responses;

namespace TradeBlotter.Application.Abstractions.Services;

public interface IPositionService
{
    Task<IReadOnlyList<PositionResponse>> GetPositionsAsync(
        CancellationToken cancellationToken);
}
