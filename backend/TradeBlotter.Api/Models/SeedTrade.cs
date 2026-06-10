using TradeBlotter.Domain;

namespace TradeBlotter.Application.Models;

public sealed record SeedTrade(
    string SeedKey,
    string Symbol,
    TradeSide Side,
    long Quantity,
    decimal Price,
    DateTimeOffset ExecutedAtUtc);
