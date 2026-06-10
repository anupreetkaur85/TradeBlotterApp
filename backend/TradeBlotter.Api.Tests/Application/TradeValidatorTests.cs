using NUnit.Framework;
using TradeBlotter.Application.Contracts.Requests;
using TradeBlotter.Application.Validation;
using TradeBlotter.Domain;

namespace TradeBlotter.UnitTests.Application;

[TestFixture]
public sealed class TradeValidatorTests
{
    [Test]
    public void Valid_request_normalizes_symbol_and_side()
    {
        var request = new CreateTradeRequest(" aapl ", "buy", 100, 187.5m);

        bool valid = TradeValidator.TryValidate(
            request,
            out var normalized,
            out var errors);

        Assert.That(valid, Is.True);
        Assert.That(errors, Is.Empty);
        Assert.That(normalized.Symbol, Is.EqualTo("AAPL"));
        Assert.That(normalized.Side, Is.EqualTo(TradeSide.Buy));
        Assert.That(normalized.Quantity, Is.EqualTo(100));
        Assert.That(normalized.Price, Is.EqualTo(187.5m));
    }

    [TestCase("", "Buy", 100, 10.0, "Symbol")]
    [TestCase("AAPL", "Hold", 100, 10.0, "Side")]
    [TestCase("AAPL", "Buy", 0, 10.0, "Quantity")]
    [TestCase("AAPL", "Buy", -5, 10.0, "Quantity")]
    [TestCase("AAPL", "Buy", 100, 0.0, "Price")]
    [TestCase("AAPL", "Buy", 100, -1.0, "Price")]
    public void Invalid_request_reports_the_offending_field(
        string symbol,
        string side,
        long quantity,
        double price,
        string field)
    {
        var request = new CreateTradeRequest(
            symbol,
            side,
            quantity,
            (decimal)price);

        bool valid = TradeValidator.TryValidate(
            request,
            out _,
            out var errors);

        Assert.That(valid, Is.False);
        Assert.That(errors.ContainsKey(field), Is.True);
    }

    [TestCase("1.23456")]
    [TestCase("1000000000000000")]
    public void Invalid_price_precision_or_range_is_rejected(string price)
    {
        var request = new CreateTradeRequest(
            "AAPL",
            "Buy",
            1,
            decimal.Parse(
                price,
                System.Globalization.CultureInfo.InvariantCulture));

        bool valid = TradeValidator.TryValidate(
            request,
            out _,
            out var errors);

        Assert.That(valid, Is.False);
        Assert.That(errors.ContainsKey("Price"), Is.True);
    }
}
