namespace TradeBlotter.Application.Options;

public sealed class SeedingOptions
{
    public const string SectionName = "Seeding";

    public bool Enabled { get; set; }

    public bool RunOnStartup { get; set; }
}
