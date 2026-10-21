namespace JobApplicationAgent.Collection.Infrastructure.Connectors;

public sealed class AdzunaOptions
{
    public string? AppId { get; set; }
    public string? AppKey { get; set; }
    public string Country { get; set; } = "fr";
    public int ResultsPerPage { get; set; } = 20;
    public AdzunaSyncOptions Sync { get; set; } = new();
}

public sealed class AdzunaSyncOptions
{
    public bool Enabled { get; set; }
    public int IntervalMinutes { get; set; } = 360;
    public List<AdzunaSearchOptions> Searches { get; set; } = [];
}

public sealed class AdzunaSearchOptions
{
    public string? What { get; set; }
    public string? Where { get; set; }
}
