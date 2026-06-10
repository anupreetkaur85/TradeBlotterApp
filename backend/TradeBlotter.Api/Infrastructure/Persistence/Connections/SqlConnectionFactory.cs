using System.Data.Common;
using Microsoft.Data.SqlClient;
using TradeBlotter.Application.Abstractions.Persistence;

namespace TradeBlotter.Infrastructure.Persistence.Connections;

public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(string connectionString) => _connectionString = connectionString;

    public async Task<DbConnection> OpenAsync(CancellationToken ct)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }
}
