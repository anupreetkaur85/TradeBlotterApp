using Microsoft.Data.SqlClient;
using Polly;
using Polly.Retry;

namespace TradeBlotter.Infrastructure.Persistence.Resilience;

/// <summary>
/// A bounded exponential-backoff retry pipeline for transient SQL faults
/// (connection drops, timeouts, deadlocks, throttling). Applied around the Dapper
/// data-access calls so transient errors recover instead of surfacing as 500s.
/// </summary>
public static class DatabaseResilience
{
    public static ResiliencePipeline CreatePipeline() =>
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<SqlException>(TransientSqlErrorDetector.IsTransient)
                    .Handle<TimeoutException>(),
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(200),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
            })
            .Build();
}

internal static class TransientSqlErrorDetector
{
    // Representative transient SQL Server / Azure SQL error numbers.
    private static readonly HashSet<int> TransientNumbers =
    [
        -2,    // timeout
        20, 64, 233, 1205, // connection / deadlock victim
        4060, 10053, 10054, 10060,
        10928, 10929, // resource governor limits
        40197, 40501, 40613, 49918, 49919, 49920,
    ];

    public static bool IsTransient(SqlException exception)
    {
        foreach (SqlError error in exception.Errors)
        {
            if (TransientNumbers.Contains(error.Number))
            {
                return true;
            }
        }

        return false;
    }
}
