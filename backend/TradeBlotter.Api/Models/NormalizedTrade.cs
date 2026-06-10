using TradeBlotter.Domain;

namespace TradeBlotter.Application.Models;

public readonly record struct NormalizedTrade(
    string Symbol,
    TradeSide Side,
    long Quantity,
    decimal Price);
