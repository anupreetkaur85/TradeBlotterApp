namespace TradeBlotter.Application.Contracts.Responses;

public sealed record PositionResponse(
    string Symbol,
    long NetQuantity,
    decimal AverageCost);
