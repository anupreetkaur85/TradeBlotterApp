using System.Data.Common;

namespace TradeBlotter.Application.Abstractions.Persistence;

public interface ISqlConnectionFactory
{
    Task<DbConnection> OpenAsync(CancellationToken cancellationToken);
}
