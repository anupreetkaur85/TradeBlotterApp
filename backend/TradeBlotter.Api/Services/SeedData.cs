using TradeBlotter.Application.Models;
using TradeBlotter.Domain;

namespace TradeBlotter.Application.Services;

/// <summary>
/// Deterministic sample trades used by the optional seed admin endpoint. Each row
/// has a stable <see cref="SeedTrade.SeedKey"/> so enabling is idempotent and
/// disabling removes exactly these rows. Includes mixed buy/sell flows so the
/// positions panel has something interesting (a weighted average and a short).
/// </summary>
public static class SeedData
{
    private static readonly DateTimeOffset Origin = new(2026, 1, 2, 14, 30, 0, TimeSpan.Zero);

    public static readonly IReadOnlyList<SeedTrade> Rows = new[]
    {
        new SeedTrade("AAPL-01", "AAPL", TradeSide.Buy,  100, 185.00m, Origin.AddMinutes(0)),
        new SeedTrade("AAPL-02", "AAPL", TradeSide.Buy,  100, 195.00m, Origin.AddMinutes(5)),
        new SeedTrade("AAPL-03", "AAPL", TradeSide.Sell,  50, 210.00m, Origin.AddMinutes(9)),
        new SeedTrade("MSFT-01", "MSFT", TradeSide.Buy,  200, 410.50m, Origin.AddMinutes(2)),
        new SeedTrade("MSFT-02", "MSFT", TradeSide.Sell,  80, 425.25m, Origin.AddMinutes(7)),
        new SeedTrade("TSLA-01", "TSLA", TradeSide.Sell, 150, 245.00m, Origin.AddMinutes(3)),
        new SeedTrade("TSLA-02", "TSLA", TradeSide.Buy,   50, 240.00m, Origin.AddMinutes(8)),
        new SeedTrade("NVDA-01", "NVDA", TradeSide.Buy,  300, 120.10m, Origin.AddMinutes(4)),
    };
}
