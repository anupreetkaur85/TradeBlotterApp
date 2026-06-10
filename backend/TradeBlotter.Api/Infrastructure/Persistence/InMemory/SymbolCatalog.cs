using TradeBlotter.Application.Contracts.Responses;

namespace TradeBlotter.Infrastructure.Persistence.InMemory;

/// <summary>
/// The reference symbol universe for the in-memory provider. Mirrors the rows in
/// migration 005 so autocomplete behaves the same with or without SQL Server.
/// </summary>
internal static class SymbolCatalog
{
    private static readonly SymbolResponse[] All =
    [
        new("AAPL", "Apple Inc."),
        new("MSFT", "Microsoft Corporation"),
        new("GOOGL", "Alphabet Inc. Class A"),
        new("GOOG", "Alphabet Inc. Class C"),
        new("AMZN", "Amazon.com, Inc."),
        new("NVDA", "NVIDIA Corporation"),
        new("META", "Meta Platforms, Inc."),
        new("TSLA", "Tesla, Inc."),
        new("BRK.B", "Berkshire Hathaway Inc. Class B"),
        new("JPM", "JPMorgan Chase & Co."),
        new("V", "Visa Inc."),
        new("MA", "Mastercard Incorporated"),
        new("UNH", "UnitedHealth Group Incorporated"),
        new("HD", "The Home Depot, Inc."),
        new("PG", "The Procter & Gamble Company"),
        new("JNJ", "Johnson & Johnson"),
        new("XOM", "Exxon Mobil Corporation"),
        new("CVX", "Chevron Corporation"),
        new("KO", "The Coca-Cola Company"),
        new("PEP", "PepsiCo, Inc."),
        new("COST", "Costco Wholesale Corporation"),
        new("WMT", "Walmart Inc."),
        new("DIS", "The Walt Disney Company"),
        new("NFLX", "Netflix, Inc."),
        new("ADBE", "Adobe Inc."),
        new("CRM", "Salesforce, Inc."),
        new("INTC", "Intel Corporation"),
        new("AMD", "Advanced Micro Devices, Inc."),
        new("ORCL", "Oracle Corporation"),
        new("CSCO", "Cisco Systems, Inc."),
        new("BAC", "Bank of America Corporation"),
        new("WFC", "Wells Fargo & Company"),
        new("GS", "The Goldman Sachs Group, Inc."),
        new("MS", "Morgan Stanley"),
        new("BA", "The Boeing Company"),
        new("PFE", "Pfizer Inc."),
        new("T", "AT&T Inc."),
        new("VZ", "Verizon Communications Inc."),
        new("NKE", "NIKE, Inc."),
        new("PYPL", "PayPal Holdings, Inc."),
    ];

    /// <summary>Ranked search mirroring usp_Symbol_Search (exact, prefix, contains, name).</summary>
    public static IReadOnlyList<SymbolResponse> Search(string query, int limit)
    {
        string q = (query ?? string.Empty).Trim().ToUpperInvariant();

        if (q.Length == 0)
        {
            var top = new List<SymbolResponse>(All);
            top.Sort(static (a, b) => string.CompareOrdinal(a.Symbol, b.Symbol));
            return top.Count <= limit ? top : top.GetRange(0, limit);
        }

        var ranked = new List<(SymbolResponse Option, int Rank)>();
        foreach (SymbolResponse option in All)
        {
            string symbol = option.Symbol.ToUpperInvariant();
            string name = option.Name.ToUpperInvariant();

            int rank = symbol == q ? 0
                : symbol.StartsWith(q, StringComparison.Ordinal) ? 1
                : symbol.Contains(q, StringComparison.Ordinal) ? 2
                : name.Contains(q, StringComparison.Ordinal) ? 3
                : -1;

            if (rank >= 0) ranked.Add((option, rank));
        }

        ranked.Sort(static (a, b) =>
        {
            if (a.Rank != b.Rank) return a.Rank - b.Rank;
            int byLen = a.Option.Symbol.Length - b.Option.Symbol.Length;
            return byLen != 0 ? byLen : string.CompareOrdinal(a.Option.Symbol, b.Option.Symbol);
        });

        var results = new List<SymbolResponse>(Math.Min(limit, ranked.Count));
        for (int i = 0; i < ranked.Count && results.Count < limit; i++)
        {
            results.Add(ranked[i].Option);
        }

        return results;
    }
}
