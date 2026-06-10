using System.Data;
using System.Data.Common;
using Dapper;
using Polly;
using TradeBlotter.Application.Abstractions.Persistence;
using TradeBlotter.Application.Contracts.Responses;

namespace TradeBlotter.Infrastructure.Persistence.Repositories;

/// <summary>Dapper-backed symbol search via the <c>usp_Symbol_Search</c> sproc.</summary>
public sealed class SymbolRepository : ISymbolRepository
{
    private const int CommandTimeoutSeconds = 5;
    private readonly ISqlConnectionFactory _factory;
    private readonly ResiliencePipeline _pipeline;

    public SymbolRepository(ISqlConnectionFactory factory, ResiliencePipeline pipeline)
    {
        _factory = factory;
        _pipeline = pipeline;
    }

    public async Task<IReadOnlyList<SymbolResponse>> SearchAsync(string query, int limit, CancellationToken ct)
    {
        return await _pipeline.ExecuteAsync<IReadOnlyList<SymbolResponse>>(async token =>
        {
            await using DbConnection conn = await _factory.OpenAsync(token);
            var cmd = new CommandDefinition(
                "dbo.usp_Symbol_Search",
                new { Query = query, Limit = limit },
                commandType: CommandType.StoredProcedure,
                commandTimeout: CommandTimeoutSeconds,
                cancellationToken: token);

            IEnumerable<SymbolResponse> rows = await conn.QueryAsync<SymbolResponse>(cmd);
            return new List<SymbolResponse>(rows);
        }, ct);
    }
}
