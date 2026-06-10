using TradeBlotter.Application.Abstractions.Persistence;
using TradeBlotter.Application.Abstractions.Services;
using TradeBlotter.Application.Contracts.Responses;
using TradeBlotter.Application.Contracts.Results;
using TradeBlotter.Application.Models;
using TradeBlotter.Domain;

namespace TradeBlotter.Application.Services;

public sealed class TradeService : ITradeService
{
    private readonly ITradeRepository _repository;

    public TradeService(ITradeRepository repository) => _repository = repository;

    public async Task<CreateTradeResult> CreateAsync(NormalizedTrade trade, Guid? clientRequestId, CancellationToken ct)
    {
        // The server is authoritative for the execution timestamp.
        DateTimeOffset executedAt = DateTimeOffset.UtcNow;
        TradeInsertResult result = await _repository.InsertAsync(trade, executedAt, clientRequestId, ct);
        return new CreateTradeResult(Map(result.Trade), result.Created);
    }

    public async Task<IReadOnlyList<TradeResponse>> GetTradesAsync(CancellationToken ct)
    {
        IReadOnlyList<Trade> trades = await _repository.GetNewestFirstAsync(ct);

        var items = new List<TradeResponse>(trades.Count);
        for (int i = 0; i < trades.Count; i++)
        {
            items.Add(Map(trades[i]));
        }

        return items;
    }

    internal static TradeResponse Map(Trade t) =>
        new(t.TradeId, t.Symbol, t.Side.ToString(), t.Quantity, t.Price, t.Notional, t.ExecutedAtUtc);
}
