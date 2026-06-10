using TradeBlotter.Application.Models;
using TradeBlotter.Domain;

namespace TradeBlotter.Application.Abstractions.Persistence;

public interface ITradeRepository
{
    Task<TradeInsertResult> InsertAsync(
        NormalizedTrade trade,
        DateTimeOffset executedAtUtc,
        Guid? clientRequestId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Trade>> GetNewestFirstAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PositionTrade>> GetForPositionsAsync(
        CancellationToken cancellationToken);

    Task<int> CountSeedRowsAsync(CancellationToken cancellationToken);

    Task<int> InsertSeedRowsAsync(
        IReadOnlyList<SeedTrade> rows,
        CancellationToken cancellationToken);

    Task<int> DeleteSeedRowsAsync(CancellationToken cancellationToken);
}
