using TradeBlotter.Application.Contracts.Responses;

namespace TradeBlotter.Application.Contracts.Results;

public sealed record CreateTradeResult(TradeResponse Trade, bool Created);
