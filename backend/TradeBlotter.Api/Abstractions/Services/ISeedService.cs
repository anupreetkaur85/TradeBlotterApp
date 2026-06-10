using TradeBlotter.Application.Models;

namespace TradeBlotter.Application.Abstractions.Services;

public interface ISeedService
{
    Task<SeedStatus> GetStatusAsync(CancellationToken cancellationToken);

    Task<SeedStatus> SetEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken);
}
