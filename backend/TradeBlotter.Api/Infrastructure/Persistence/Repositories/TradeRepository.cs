using System.Data;
using System.Data.Common;
using Dapper;
using Microsoft.Data.SqlClient;
using Polly;
using TradeBlotter.Application.Abstractions.Persistence;
using TradeBlotter.Application.Models;
using TradeBlotter.Domain;

namespace TradeBlotter.Infrastructure.Persistence.Repositories;

/// <summary>
/// Dapper-based trade store that calls stored procedures (no inline SQL on the
/// request path, no LINQ-to-entities). Side is persisted as CHAR(1) ('B'/'S') and
/// mapped to the enum on read. Reads and idempotent writes use the transient-fault
/// retry pipeline; an insert without an idempotency key is never retried after an
/// ambiguous failure.
/// </summary>
public sealed class TradeRepository : ITradeRepository
{
    private const int CommandTimeoutSeconds = 10;
    private readonly ISqlConnectionFactory _factory;
    private readonly ResiliencePipeline _pipeline;

    public TradeRepository(ISqlConnectionFactory factory, ResiliencePipeline pipeline)
    {
        _factory = factory;
        _pipeline = pipeline;
    }

    public async Task<TradeInsertResult> InsertAsync(
        NormalizedTrade trade,
        DateTimeOffset executedAtUtc,
        Guid? clientRequestId,
        CancellationToken ct)
    {
        if (clientRequestId is null)
        {
            // A retry after an ambiguous commit could duplicate a trade. Automatic
            // retries are therefore allowed only when a caller supplies an
            // idempotency key.
            return await InsertCoreAsync(
                trade,
                executedAtUtc,
                clientRequestId,
                ct);
        }

        return await _pipeline.ExecuteAsync(
            token => InsertCoreAsync(
                trade,
                executedAtUtc,
                clientRequestId,
                token),
            ct);
    }

    private async ValueTask<TradeInsertResult> InsertCoreAsync(
        NormalizedTrade trade,
        DateTimeOffset executedAtUtc,
        Guid? clientRequestId,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection =
            await _factory.OpenAsync(cancellationToken);

        if (clientRequestId is Guid key)
        {
            Trade? existing = await QuerySingleTradeAsync(
                connection,
                "dbo.usp_Trade_GetByClientRequestId",
                new { ClientRequestId = key },
                null,
                cancellationToken);
            if (existing is not null)
            {
                return new TradeInsertResult(existing, Created: false);
            }
        }

        var parameters = new
        {
            trade.Symbol,
            Side = ToSideChar(trade.Side),
            trade.Quantity,
            trade.Price,
            ExecutedAtUtc = executedAtUtc,
            ClientRequestId = clientRequestId,
        };

        try
        {
            Trade inserted = (await QuerySingleTradeAsync(
                connection,
                "dbo.usp_Trade_Insert",
                parameters,
                null,
                cancellationToken))!;
            return new TradeInsertResult(inserted, Created: true);
        }
        catch (SqlException exception) when (
            clientRequestId is Guid replayKey
            && IsUniqueViolation(exception))
        {
            Trade? winner = await QuerySingleTradeAsync(
                connection,
                "dbo.usp_Trade_GetByClientRequestId",
                new { ClientRequestId = replayKey },
                null,
                cancellationToken);
            if (winner is not null)
            {
                return new TradeInsertResult(winner, Created: false);
            }

            throw;
        }
    }

    public async Task<IReadOnlyList<Trade>> GetNewestFirstAsync(CancellationToken ct)
    {
        return await _pipeline.ExecuteAsync<IReadOnlyList<Trade>>(async token =>
        {
            await using DbConnection conn = await _factory.OpenAsync(token);
            var cmd = StoredProcedure("dbo.usp_Trade_GetNewestFirst", null, null, token);

            IEnumerable<TradeRow> rows = await conn.QueryAsync<TradeRow>(cmd);
            var list = new List<Trade>();
            foreach (TradeRow row in rows)
            {
                list.Add(ToTrade(row));
            }

            return list;
        }, ct);
    }

    public async Task<IReadOnlyList<PositionTrade>> GetForPositionsAsync(CancellationToken ct)
    {
        return await _pipeline.ExecuteAsync<IReadOnlyList<PositionTrade>>(async token =>
        {
            await using DbConnection conn = await _factory.OpenAsync(token);
            var cmd = StoredProcedure("dbo.usp_Trade_GetForPositions", null, null, token);

            IEnumerable<PositionRow> rows = await conn.QueryAsync<PositionRow>(cmd);
            var list = new List<PositionTrade>();
            foreach (PositionRow row in rows)
            {
                list.Add(new PositionTrade(row.Symbol, FromSideChar(row.Side), row.Quantity, row.Price));
            }

            return list;
        }, ct);
    }

    public async Task<int> CountSeedRowsAsync(CancellationToken ct)
    {
        return await _pipeline.ExecuteAsync<int>(async token =>
        {
            await using DbConnection conn = await _factory.OpenAsync(token);
            var cmd = StoredProcedure("dbo.usp_Seed_Count", null, null, token);
            return await conn.ExecuteScalarAsync<int>(cmd);
        }, ct);
    }

    public async Task<int> InsertSeedRowsAsync(IReadOnlyList<SeedTrade> rows, CancellationToken ct)
    {
        return await _pipeline.ExecuteAsync<int>(async token =>
        {
            await using DbConnection conn = await _factory.OpenAsync(token);
            // Serializable plus the unique SeedKey index coordinates competing
            // seed enables. User rows have no SeedKey and remain independent.
            await using DbTransaction tx = await conn.BeginTransactionAsync(IsolationLevel.Serializable, token);

            int affected = 0;
            foreach (SeedTrade row in rows)
            {
                var parameters = new
                {
                    row.Symbol,
                    Side = ToSideChar(row.Side),
                    row.Quantity,
                    row.Price,
                    row.ExecutedAtUtc,
                    row.SeedKey,
                };

                var cmd = StoredProcedure("dbo.usp_Seed_Insert", parameters, tx, token);
                affected += await conn.ExecuteAsync(cmd);
            }

            await tx.CommitAsync(token);
            return affected;
        }, ct);
    }

    public async Task<int> DeleteSeedRowsAsync(CancellationToken ct)
    {
        return await _pipeline.ExecuteAsync<int>(async token =>
        {
            await using DbConnection conn = await _factory.OpenAsync(token);
            await using DbTransaction tx = await conn.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var cmd = StoredProcedure("dbo.usp_Seed_Delete", null, tx, token);
            int affected = await conn.ExecuteAsync(cmd);
            await tx.CommitAsync(token);
            return affected;
        }, ct);
    }

    private static CommandDefinition StoredProcedure(string name, object? parameters, DbTransaction? tx, CancellationToken token) =>
        new(name, parameters, transaction: tx, commandTimeout: CommandTimeoutSeconds,
            commandType: CommandType.StoredProcedure, cancellationToken: token);

    private static async Task<Trade?> QuerySingleTradeAsync(
        DbConnection conn, string proc, object parameters, DbTransaction? tx, CancellationToken token)
    {
        CommandDefinition cmd = StoredProcedure(proc, parameters, tx, token);
        TradeRow? row = await conn.QueryFirstOrDefaultAsync<TradeRow>(cmd);
        return row is null ? null : ToTrade(row);
    }

    private static Trade ToTrade(TradeRow r) =>
        new(r.TradeId, r.Symbol, FromSideChar(r.Side), r.Quantity, r.Price, r.ExecutedAtUtc);

    private static string ToSideChar(TradeSide side) => side == TradeSide.Buy ? "B" : "S";

    private static TradeSide FromSideChar(string side) => side == "B" ? TradeSide.Buy : TradeSide.Sell;

    private static bool IsUniqueViolation(SqlException ex)
    {
        foreach (SqlError error in ex.Errors)
        {
            if (error.Number is 2601 or 2627)
            {
                return true;
            }
        }

        return false;
    }

    private sealed class TradeRow
    {
        public long TradeId { get; init; }
        public string Symbol { get; init; } = string.Empty;
        public string Side { get; init; } = string.Empty;
        public long Quantity { get; init; }
        public decimal Price { get; init; }
        public DateTimeOffset ExecutedAtUtc { get; init; }
    }

    private sealed class PositionRow
    {
        public string Symbol { get; init; } = string.Empty;
        public string Side { get; init; } = string.Empty;
        public long Quantity { get; init; }
        public decimal Price { get; init; }
    }
}
