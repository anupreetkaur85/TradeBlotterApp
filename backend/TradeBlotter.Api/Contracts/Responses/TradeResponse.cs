namespace TradeBlotter.Application.Contracts.Responses;

public sealed record TradeResponse(
    long TradeId,
    string Symbol,
    string Side,
    long Quantity,
    decimal Price,
    decimal Notional,
    DateTimeOffset Timestamp);
