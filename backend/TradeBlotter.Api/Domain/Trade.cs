namespace TradeBlotter.Domain;

/// <summary>
/// An immutable ledger entry. Quantity is a positive whole-share count; the
/// server assigns <see cref="ExecutedAtUtc"/> at insert time.
/// </summary>
public sealed record Trade(
    long TradeId,
    string Symbol,
    TradeSide Side,
    long Quantity,
    decimal Price,
    DateTimeOffset ExecutedAtUtc)
{
    public decimal Notional => Quantity * Price;
}
