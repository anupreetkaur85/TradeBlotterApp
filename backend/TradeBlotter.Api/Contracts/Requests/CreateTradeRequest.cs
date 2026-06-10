namespace TradeBlotter.Application.Contracts.Requests;

public sealed record CreateTradeRequest(
    string Symbol,
    string Side,
    long Quantity,
    decimal Price);
