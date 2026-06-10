using TradeBlotter.Application.Contracts.Responses;

namespace TradeBlotter.Application.Abstractions.Services;

/// <summary>
/// Enqueues real-time notifications after committed state changes. Implementations
/// must not block or throw because notification delivery is not part of the
/// originating transaction.
/// </summary>
public interface ITradeNotifier
{
    void TradeCreated(TradeResponse trade);

    void BlotterChanged();
}
