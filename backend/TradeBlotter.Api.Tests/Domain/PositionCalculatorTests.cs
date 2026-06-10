using NUnit.Framework;
using TradeBlotter.Domain;

namespace TradeBlotter.UnitTests.Domain;

[TestFixture]
public sealed class PositionCalculatorTests
{
    private static PositionTrade Buy(string symbol, long quantity, decimal price) =>
        new(symbol, TradeSide.Buy, quantity, price);

    private static PositionTrade Sell(string symbol, long quantity, decimal price) =>
        new(symbol, TradeSide.Sell, quantity, price);

    private static Position Single(
        IReadOnlyList<Position> positions,
        string symbol)
    {
        Position? found = null;
        foreach (Position position in positions)
        {
            if (position.Symbol == symbol)
            {
                found = position;
            }
        }

        Assert.That(found, Is.Not.Null, $"Expected a position for {symbol}.");
        return found!;
    }

    [Test]
    public void Empty_input_yields_no_positions()
    {
        IReadOnlyList<Position> result =
            PositionCalculator.Calculate(Array.Empty<PositionTrade>());

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Single_buy_sets_quantity_and_average_to_trade()
    {
        IReadOnlyList<Position> result =
            PositionCalculator.Calculate([Buy("AAPL", 100, 10m)]);

        Position position = Single(result, "AAPL");
        Assert.That(position.NetQuantity, Is.EqualTo(100));
        Assert.That(position.AverageCost, Is.EqualTo(10m));
    }

    [Test]
    public void Two_buys_at_different_prices_weight_the_average()
    {
        IReadOnlyList<Position> result = PositionCalculator.Calculate(
        [
            Buy("AAPL", 100, 10m),
            Buy("AAPL", 100, 20m),
        ]);

        Position position = Single(result, "AAPL");
        Assert.That(position.NetQuantity, Is.EqualTo(200));
        Assert.That(position.AverageCost, Is.EqualTo(15m));
    }

    [Test]
    public void Partial_sell_leaves_average_unchanged()
    {
        IReadOnlyList<Position> result = PositionCalculator.Calculate(
        [
            Buy("AAPL", 100, 10m),
            Buy("AAPL", 100, 20m),
            Sell("AAPL", 50, 30m),
        ]);

        Position position = Single(result, "AAPL");
        Assert.That(position.NetQuantity, Is.EqualTo(150));
        Assert.That(position.AverageCost, Is.EqualTo(15m));
    }

    [Test]
    public void Exact_close_omits_the_symbol()
    {
        IReadOnlyList<Position> result = PositionCalculator.Calculate(
        [
            Buy("AAPL", 100, 10m),
            Sell("AAPL", 100, 12m),
        ]);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Oversell_flips_to_short_at_the_crossing_price()
    {
        IReadOnlyList<Position> result = PositionCalculator.Calculate(
        [
            Buy("AAPL", 100, 10m),
            Sell("AAPL", 150, 20m),
        ]);

        Position position = Single(result, "AAPL");
        Assert.That(position.NetQuantity, Is.EqualTo(-50));
        Assert.That(position.AverageCost, Is.EqualTo(20m));
    }

    [Test]
    public void Partial_cover_of_short_leaves_average_unchanged()
    {
        IReadOnlyList<Position> result = PositionCalculator.Calculate(
        [
            Sell("AAPL", 100, 50m),
            Buy("AAPL", 40, 45m),
        ]);

        Position position = Single(result, "AAPL");
        Assert.That(position.NetQuantity, Is.EqualTo(-60));
        Assert.That(position.AverageCost, Is.EqualTo(50m));
    }

    [Test]
    public void Short_fully_covered_is_omitted()
    {
        IReadOnlyList<Position> result = PositionCalculator.Calculate(
        [
            Sell("AAPL", 100, 50m),
            Buy("AAPL", 100, 45m),
        ]);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Short_over_covered_flips_to_long_at_the_crossing_price()
    {
        IReadOnlyList<Position> result = PositionCalculator.Calculate(
        [
            Sell("AAPL", 100, 50m),
            Buy("AAPL", 160, 45m),
        ]);

        Position position = Single(result, "AAPL");
        Assert.That(position.NetQuantity, Is.EqualTo(60));
        Assert.That(position.AverageCost, Is.EqualTo(45m));
    }

    [Test]
    public void Multiple_symbols_fold_independently()
    {
        IReadOnlyList<Position> result = PositionCalculator.Calculate(
        [
            Buy("AAPL", 100, 10m),
            Buy("MSFT", 200, 400m),
            Sell("AAPL", 100, 12m),
        ]);

        Assert.That(result, Has.Count.EqualTo(1));
        Position msft = Single(result, "MSFT");
        Assert.That(msft.NetQuantity, Is.EqualTo(200));
        Assert.That(msft.AverageCost, Is.EqualTo(400m));
    }

    [Test]
    public void Long_then_cross_through_zero_to_short_matches_design_example()
    {
        IReadOnlyList<Position> result = PositionCalculator.Calculate(
        [
            Buy("AAPL", 100, 10m),
            Buy("AAPL", 100, 20m),
            Sell("AAPL", 50, 30m),
            Sell("AAPL", 200, 18m),
        ]);

        Position position = Single(result, "AAPL");
        Assert.That(position.NetQuantity, Is.EqualTo(-50));
        Assert.That(position.AverageCost, Is.EqualTo(18m));
    }

    [Test]
    public void Quantity_overflow_is_not_silently_wrapped()
    {
        PositionTrade[] trades =
        [
            Buy("AAPL", long.MaxValue, 10m),
            Buy("AAPL", 1, 10m),
        ];

        Assert.That(
            () => PositionCalculator.Calculate(trades),
            Throws.TypeOf<OverflowException>());
    }
}
