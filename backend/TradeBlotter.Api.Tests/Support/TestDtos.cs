namespace TradeBlotter.Api.IntegrationTests.Infrastructure;

public sealed record TradeDto(
    long TradeId,
    string Symbol,
    string Side,
    long Quantity,
    decimal Price,
    decimal Notional,
    DateTimeOffset Timestamp);

public sealed record PositionDto(string Symbol, long NetQuantity, decimal AverageCost);

public sealed record SeedStatusDto(bool Enabled, int SeedRowCount);
