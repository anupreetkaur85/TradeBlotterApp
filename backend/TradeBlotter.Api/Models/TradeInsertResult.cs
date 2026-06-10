using TradeBlotter.Domain;

namespace TradeBlotter.Application.Models;

public sealed record TradeInsertResult(Trade Trade, bool Created);
