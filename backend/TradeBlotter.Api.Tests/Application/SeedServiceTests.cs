using Microsoft.Extensions.Logging.Abstractions;
using TradeBlotter.Application.Abstractions.Persistence;
using TradeBlotter.Application.Abstractions.Services;
using TradeBlotter.Application.Contracts.Responses;
using TradeBlotter.Application.Models;
using TradeBlotter.Application.Services;
using TradeBlotter.Domain;

namespace TradeBlotter.UnitTests.Application;

[TestFixture]
public sealed class SeedServiceTests
{
    [Test]
    public async Task SetEnabled_notifies_when_seed_rows_change()
    {
        var repository = new StubTradeRepository { InsertAffected = 3 };
        var notifier = new RecordingNotifier();
        var service = new SeedService(
            repository,
            notifier,
            NullLogger<SeedService>.Instance);

        SeedStatus status = await service.SetEnabledAsync(true, CancellationToken.None);

        Assert.That(status.Enabled, Is.True);
        Assert.That(status.SeedRowCount, Is.EqualTo(3));
        Assert.That(notifier.BlotterChangedCount, Is.EqualTo(1));
    }

    [Test]
    public async Task SetEnabled_does_not_notify_when_state_is_unchanged()
    {
        var repository = new StubTradeRepository
        {
            SeedRowCount = 3,
            InsertAffected = 0,
        };
        var notifier = new RecordingNotifier();
        var service = new SeedService(
            repository,
            notifier,
            NullLogger<SeedService>.Instance);

        SeedStatus status = await service.SetEnabledAsync(true, CancellationToken.None);

        Assert.That(status.Enabled, Is.True);
        Assert.That(notifier.BlotterChangedCount, Is.Zero);
    }

    private sealed class RecordingNotifier : ITradeNotifier
    {
        public int BlotterChangedCount { get; private set; }

        public void TradeCreated(TradeResponse trade)
        {
        }

        public void BlotterChanged() => BlotterChangedCount++;
    }

    private sealed class StubTradeRepository : ITradeRepository
    {
        public int SeedRowCount { get; set; }
        public int InsertAffected { get; init; }

        public Task<int> CountSeedRowsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(SeedRowCount);

        public Task<int> InsertSeedRowsAsync(
            IReadOnlyList<SeedTrade> rows,
            CancellationToken cancellationToken)
        {
            SeedRowCount += InsertAffected;
            return Task.FromResult(InsertAffected);
        }

        public Task<int> DeleteSeedRowsAsync(CancellationToken cancellationToken)
        {
            int deleted = SeedRowCount;
            SeedRowCount = 0;
            return Task.FromResult(deleted);
        }

        public Task<TradeInsertResult> InsertAsync(
            NormalizedTrade trade,
            DateTimeOffset executedAtUtc,
            Guid? clientRequestId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Trade>> GetNewestFirstAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<PositionTrade>> GetForPositionsAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
