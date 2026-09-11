namespace NetSpectatr;

public sealed class NetSpectatrOptions
{
    public string? ServiceName { get; set; }
    public string? ServiceVersion { get; set; }
    public string? ServiceNamespace { get; set; }
    public double SamplingRatio { get; set; } = 0.1;
    // Detailed exception text is opt-in. Type and error status remain available.
    public bool CaptureExceptionDetails { get; set; }
    // Upstream EF Core instrumentation is beta and explicitly opt-in.
    public bool EnableEntityFrameworkCore { get; set; }
    public bool EnableSqlClient { get; set; } = true;
    public string[] ActivitySources { get; set; } = [];
    public string[] Meters { get; set; } = [];
}
