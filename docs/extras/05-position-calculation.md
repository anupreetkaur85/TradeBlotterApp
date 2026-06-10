# Position Calculation

The position algorithm is the most important domain behavior in the
assessment. It must be deterministic, order-aware, and independently tested.

## Definitions

- Net quantity is signed: positive is long and negative is short.
- Average cost is the moving weighted average cost of the open position.
- Quantity is a whole-share `long`.
- Price and average cost are `decimal`.
- Trades are processed by ascending `(ExecutedAtUtc, TradeId)`.
- A position with zero net quantity is omitted.

## Rules

For each trade, derive a signed quantity:

```text
Buy  -> +Quantity
Sell -> -Quantity
```

Then apply:

1. Flat position: open at the trade price.
2. Same direction: add quantity and recalculate weighted average.
3. Opposite direction without crossing zero: reduce quantity and retain
   average cost.
4. Exact close: set quantity and average cost to zero.
5. Cross through zero: open the residual opposite position at the crossing
   trade's price.

Weighted average:

```text
(abs(old quantity) * old average + trade quantity * trade price)
/ abs(new quantity)
```

Keep full decimal precision during the fold. Round only at the response/display
boundary if a four-decimal contract is selected.

## Worked example

| Trade | Net quantity | Average cost |
|---|---:|---:|
| Buy 100 @ 10 | 100 | 10 |
| Buy 100 @ 20 | 200 | 15 |
| Sell 50 @ 30 | 150 | 15 |
| Sell 200 @ 18 | -50 | 18 |

The partial sell does not alter the long position's cost basis. The final sell
closes the remaining 150 long shares and opens 50 short shares at 18.

## Implementation shape

Use one dictionary keyed by normalized symbol and one explicit loop:

```csharp
public static List<Position> Calculate(IReadOnlyList<PositionTrade> trades)
{
    var states = new Dictionary<string, PositionState>(
        StringComparer.Ordinal);

    for (var i = 0; i < trades.Count; i++)
    {
        var trade = trades[i];
        states.TryGetValue(trade.Symbol, out var state);

        var delta = trade.Side == TradeSide.Buy
            ? trade.Quantity
            : checked(-trade.Quantity);

        state.Apply(delta, trade.Price);
        states[trade.Symbol] = state;
    }

    var result = new List<Position>(states.Count);
    foreach (var pair in states)
    {
        if (pair.Value.NetQuantity != 0)
        {
            result.Add(new Position(
                pair.Key,
                pair.Value.NetQuantity,
                pair.Value.AverageCost));
        }
    }

    result.Sort(static (left, right) =>
        string.CompareOrdinal(left.Symbol, right.Symbol));

    return result;
}
```

`PositionState.Apply` should use explicit sign/absolute-quantity comparisons.
Use checked arithmetic where quantity or notional overflow is possible. The
repository, not the calculator, owns chronological ordering.

## NUnit test matrix

| Scenario | Expected result |
|---|---|
| Empty input | Empty positions |
| Single buy | Long at trade price |
| Single sell | Short at trade price |
| Multiple buys | Correct weighted average |
| Multiple sells | Correct short weighted average |
| Partial long close | Average unchanged |
| Partial short cover | Average unchanged |
| Exact long close | Symbol omitted |
| Exact short close | Symbol omitted |
| Long-to-short cross | Residual short at crossing price |
| Short-to-long cross | Residual long at crossing price |
| Multiple symbols | Independent calculations |
| Same timestamp | Trade ID order determines result |
| Large quantity arithmetic | Overflow is controlled |
| Repeating decimal average | Full internal decimal precision retained |

Realized P&L, FIFO/LIFO lots, and trade correction are outside assessment
scope.
