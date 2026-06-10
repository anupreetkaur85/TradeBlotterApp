using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TradeBlotter.Application.Abstractions.Persistence;
using TradeBlotter.Application.Abstractions.Services;
using TradeBlotter.Application.Models;

namespace TradeBlotter.Application.Services;

public sealed class SeedService : ISeedService
{
    private readonly ITradeRepository _repository;
    private readonly ITradeNotifier _notifier;
    private readonly ILogger<SeedService> _logger;

    public SeedService(
        ITradeRepository repository,
        ITradeNotifier notifier,
        ILogger<SeedService> logger)
    {
        _repository = repository;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task<SeedStatus> GetStatusAsync(CancellationToken ct)
    {
        int count = await _repository.CountSeedRowsAsync(ct);
        return new SeedStatus(count > 0, count);
    }

    public async Task<SeedStatus> SetEnabledAsync(bool enabled, CancellationToken ct)
    {
        long started = Stopwatch.GetTimestamp();

        int affected;
        if (enabled)
        {
            // Idempotent: inserts only the seed rows whose SeedKey is missing.
            affected = await _repository.InsertSeedRowsAsync(SeedData.Rows, ct);
        }
        else
        {
            // Removes only IsSeedData = 1 rows; user trades are never touched.
            affected = await _repository.DeleteSeedRowsAsync(ct);
        }

        SeedStatus status = await GetStatusAsync(ct);
        if (affected > 0)
        {
            _notifier.BlotterChanged();
        }

        // Single operational event shared by startup and admin requests. No per-row
        // detail or SQL parameters are logged.
        _logger.LogInformation(
            "Seed data state changed: requestedEnabled={RequestedEnabled}, enabled={SeedEnabled}, seedRowCount={SeedRowCount}, elapsedMs={ElapsedMs}",
            enabled,
            status.Enabled,
            status.SeedRowCount,
            Stopwatch.GetElapsedTime(started).TotalMilliseconds);

        return status;
    }
}
