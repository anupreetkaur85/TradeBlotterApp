using TradeBlotter.Application.Abstractions.Persistence;
using TradeBlotter.Application.Models;
using TradeBlotter.Domain;

namespace TradeBlotter.Infrastructure.Persistence.InMemory;

/// <summary>
/// Thread-safe in-memory trade store mirroring the SQL repository's semantics
/// (identity ids, idempotency by client request id, seed rows, newest-first and
/// chronological ordering). Used when the in-memory provider is selected.
/// </summary>
public sealed class InMemoryTradeRepository : ITradeRepository
{
    private readonly object _gate = new();
    private readonly List<Entry> _entries = [];
    private readonly Dictionary<Guid, long> _byClientRequest = [];
    private long _nextId;

    public Task<TradeInsertResult> InsertAsync(
        NormalizedTrade trade, DateTimeOffset executedAtUtc, Guid? clientRequestId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (clientRequestId is Guid key && _byClientRequest.TryGetValue(key, out long existingId))
            {
                Trade existing = Find(existingId)!;
                return Task.FromResult(new TradeInsertResult(existing, Created: false));
            }

            var inserted = new Trade(++_nextId, trade.Symbol, trade.Side, trade.Quantity, trade.Price, executedAtUtc);
            _entries.Add(new Entry(inserted, IsSeed: false, SeedKey: null));
            if (clientRequestId is Guid k)
            {
                _byClientRequest[k] = inserted.TradeId;
            }

            return Task.FromResult(new TradeInsertResult(inserted, Created: true));
        }
    }

    public Task<IReadOnlyList<Trade>> GetNewestFirstAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var list = new List<Trade>(_entries.Count);
            foreach (Entry e in _entries)
            {
                list.Add(e.Trade);
            }

            // Newest first: ExecutedAtUtc DESC, TradeId DESC.
            list.Sort(static (a, b) =>
            {
                int byTime = b.ExecutedAtUtc.CompareTo(a.ExecutedAtUtc);
                return byTime != 0 ? byTime : b.TradeId.CompareTo(a.TradeId);
            });
            return Task.FromResult<IReadOnlyList<Trade>>(list);
        }
    }

    public Task<IReadOnlyList<PositionTrade>> GetForPositionsAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var trades = new List<Trade>(_entries.Count);
            foreach (Entry e in _entries)
            {
                trades.Add(e.Trade);
            }

            // Chronological per symbol: Symbol ASC, ExecutedAtUtc ASC, TradeId ASC.
            trades.Sort(static (a, b) =>
            {
                int bySymbol = string.CompareOrdinal(a.Symbol, b.Symbol);
                if (bySymbol != 0) return bySymbol;
                int byTime = a.ExecutedAtUtc.CompareTo(b.ExecutedAtUtc);
                return byTime != 0 ? byTime : a.TradeId.CompareTo(b.TradeId);
            });

            var result = new List<PositionTrade>(trades.Count);
            foreach (Trade t in trades)
            {
                result.Add(new PositionTrade(t.Symbol, t.Side, t.Quantity, t.Price));
            }

            return Task.FromResult<IReadOnlyList<PositionTrade>>(result);
        }
    }

    public Task<int> CountSeedRowsAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            int count = 0;
            foreach (Entry e in _entries)
            {
                if (e.IsSeed) count++;
            }

            return Task.FromResult(count);
        }
    }

    public Task<int> InsertSeedRowsAsync(IReadOnlyList<SeedTrade> rows, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            int added = 0;
            foreach (SeedTrade row in rows)
            {
                if (HasSeedKey(row.SeedKey)) continue;
                var seedTrade = new Trade(++_nextId, row.Symbol, row.Side, row.Quantity, row.Price, row.ExecutedAtUtc);
                _entries.Add(new Entry(seedTrade, IsSeed: true, row.SeedKey));
                added++;
            }

            return Task.FromResult(added);
        }
    }

    public Task<int> DeleteSeedRowsAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            int removed = _entries.RemoveAll(static e => e.IsSeed);
            return Task.FromResult(removed);
        }
    }

    private Trade? Find(long id)
    {
        foreach (Entry e in _entries)
        {
            if (e.Trade.TradeId == id) return e.Trade;
        }

        return null;
    }

    private bool HasSeedKey(string seedKey)
    {
        foreach (Entry e in _entries)
        {
            if (e.SeedKey == seedKey) return true;
        }

        return false;
    }

    private sealed record Entry(Trade Trade, bool IsSeed, string? SeedKey);
}
