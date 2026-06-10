namespace TradeBlotter.Application.Options;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// When true the service runs against an in-memory store instead of SQL Server
    /// (no database required). Intended for reviewers without SQL Server and for
    /// fast component tests. SQL Server remains the default/production path.
    /// </summary>
    public bool UseInMemory { get; set; }
}
