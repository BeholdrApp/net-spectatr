using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

// Negative control: no NetSpectatr reference, resource adapter or handshake.
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation().AddOtlpExporter())
    .WithMetrics(m => m.AddAspNetCoreInstrumentation().AddRuntimeInstrumentation().AddOtlpExporter());
var app = builder.Build();
app.MapGet("/", () => Results.Ok(new { service = "stock-otel-demo" }));
app.Run();
