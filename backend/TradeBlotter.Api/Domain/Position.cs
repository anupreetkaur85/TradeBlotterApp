namespace TradeBlotter.Domain;

/// <summary>
/// A derived position for a symbol. Never persisted: always computed by folding
/// the trade ledger. Flat symbols (NetQuantity == 0) are not emitted.
/// </summary>
public sealed record Position(
    string Symbol,
    long NetQuantity,
    decimal AverageCost);
