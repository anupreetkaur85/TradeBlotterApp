namespace TradeBlotter.Domain;

/// <summary>
/// The minimal projection of a trade the position fold needs. Keeping this
/// separate from <see cref="Trade"/> lets the calculator stay allocation-light
/// and free of identity/timestamp concerns.
/// </summary>
public readonly record struct PositionTrade(
    string Symbol,
    TradeSide Side,
    long Quantity,
    decimal Price);
