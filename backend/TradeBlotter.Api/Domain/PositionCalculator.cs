namespace TradeBlotter.Domain;

/// <summary>
/// Folds a chronologically ordered trade ledger into net positions using the
/// moving-average-cost convention with sign-flip handling. Contains no LINQ: a
/// single forward loop over the input plus a dictionary keyed by symbol.
/// </summary>
/// <remarks>
/// The caller must supply trades ordered ascending by (ExecutedAtUtc, TradeId)
/// per symbol. Global chronological order satisfies this. The calculator does
/// not sort, so it stays O(n) over a single pass.
/// </remarks>
public static class PositionCalculator
{
    public static IReadOnlyList<Position> Calculate(IReadOnlyList<PositionTrade> orderedTrades)
    {
        var acc = new Dictionary<string, State>(StringComparer.Ordinal);

        for (int i = 0; i < orderedTrades.Count; i++)
        {
            PositionTrade t = orderedTrades[i];
            long delta = t.Side == TradeSide.Buy
                ? t.Quantity
                : checked(-t.Quantity);

            acc.TryGetValue(t.Symbol, out State s); // default (0, 0) when new

            if (s.Qty == 0)
            {
                // Opening from flat: average cost is the trade price.
                s = new State(delta, t.Price);
            }
            else if (Math.Sign(s.Qty) == Math.Sign(delta))
            {
                // Increasing in the same direction: weighted average in the new shares.
                long absQty = checked(Math.Abs(s.Qty));
                long absDelta = checked(Math.Abs(delta));
                long newAbsoluteQuantity = checked(absQty + absDelta);
                s.Avg = (absQty * s.Avg + absDelta * t.Price)
                    / newAbsoluteQuantity;
                s.Qty = checked(s.Qty + delta);
            }
            else if (checked(Math.Abs(delta)) < checked(Math.Abs(s.Qty)))
            {
                // Reducing without crossing zero: average cost is unchanged.
                s.Qty = checked(s.Qty + delta);
            }
            else if (checked(Math.Abs(delta)) == checked(Math.Abs(s.Qty)))
            {
                // Exact close: position is flat and drops out of the result.
                s = new State(0, 0m);
            }
            else
            {
                // Crossing zero: the residual opens a new position at this trade's price.
                long remaining = checked(
                    Math.Abs(delta) - Math.Abs(s.Qty));
                s = new State(
                    checked(Math.Sign(delta) * remaining),
                    t.Price);
            }

            acc[t.Symbol] = s;
        }

        var result = new List<Position>(acc.Count);
        foreach (KeyValuePair<string, State> kvp in acc)
        {
            if (kvp.Value.Qty != 0)
            {
                result.Add(new Position(kvp.Key, kvp.Value.Qty, kvp.Value.Avg));
            }
        }

        result.Sort(static (left, right) =>
            string.CompareOrdinal(left.Symbol, right.Symbol));

        return result;
    }

    private struct State
    {
        public long Qty;
        public decimal Avg;

        public State(long qty, decimal avg)
        {
            Qty = qty;
            Avg = avg;
        }
    }
}
