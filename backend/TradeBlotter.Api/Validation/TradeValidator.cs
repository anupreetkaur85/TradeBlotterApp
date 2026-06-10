using TradeBlotter.Application.Contracts.Requests;
using TradeBlotter.Application.Models;
using TradeBlotter.Domain;

namespace TradeBlotter.Application.Validation;

/// <summary>
/// Hand-written validation/normalization with no reflection on the hot path.
/// Produces an RFC 7807-shaped error dictionary (field -> messages).
/// </summary>
public static class TradeValidator
{
    public const int MaxSymbolLength = 16;
    public const decimal MaxPrice = 999_999_999_999_999.9999m;

    public static bool TryValidate(
        CreateTradeRequest request,
        out NormalizedTrade normalized,
        out Dictionary<string, string[]> errors)
    {
        errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        normalized = default;

        string symbol = (request.Symbol ?? string.Empty).Trim().ToUpperInvariant();
        if (symbol.Length == 0)
        {
            Add(errors, nameof(request.Symbol), "Symbol is required.");
        }
        else if (symbol.Length > MaxSymbolLength)
        {
            Add(errors, nameof(request.Symbol), $"Symbol must be at most {MaxSymbolLength} characters.");
        }
        else if (!IsValidSymbol(symbol))
        {
            Add(errors, nameof(request.Symbol), "Symbol may contain only letters, digits, '.' and '-'.");
        }

        TradeSide side = default;
        if (!TryParseSide(request.Side, out side))
        {
            Add(errors, nameof(request.Side), "Side must be 'Buy' or 'Sell'.");
        }

        if (request.Quantity <= 0)
        {
            Add(errors, nameof(request.Quantity), "Quantity must be a positive whole number.");
        }

        if (request.Price <= 0m)
        {
            Add(errors, nameof(request.Price), "Price must be greater than zero.");
        }
        else if (request.Price > MaxPrice)
        {
            Add(errors, nameof(request.Price), $"Price must not exceed {MaxPrice}.");
        }
        else if (decimal.Round(request.Price, 4) != request.Price)
        {
            Add(errors, nameof(request.Price), "Price must have at most four decimal places.");
        }

        if (request.Quantity > 0 && request.Price > 0m)
        {
            try
            {
                _ = checked(request.Quantity * request.Price);
            }
            catch (OverflowException)
            {
                Add(
                    errors,
                    nameof(request.Price),
                    "Quantity multiplied by price exceeds the supported notional range.");
            }
        }

        if (errors.Count > 0)
        {
            return false;
        }

        normalized = new NormalizedTrade(symbol, side, request.Quantity, request.Price);
        return true;
    }

    public static bool TryParseSide(string? value, out TradeSide side)
    {
        side = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Enum.TryParse(value.Trim(), ignoreCase: true, out side)
            && Enum.IsDefined(side);
    }

    private static bool IsValidSymbol(string symbol)
    {
        for (int i = 0; i < symbol.Length; i++)
        {
            char c = symbol[i];
            bool ok = (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == '-';
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    private static void Add(Dictionary<string, string[]> errors, string field, string message)
    {
        errors[field] = new[] { message };
    }
}
