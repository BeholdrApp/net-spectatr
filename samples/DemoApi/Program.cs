using NetSpectatr;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddNetSpectatr();
builder.Services.AddHttpClient();
var app = builder.Build();
app.MapGet("/", () => Results.Ok(new { service = "net-spectatr-demo", telemetry = "standard OTLP" }));
app.MapGet("/health", () => Results.Ok(new { status = "ready" }));
app.MapGet("/work", async (IHttpClientFactory clients, HttpContext context) =>
{
    // Exercise upstream outbound instrumentation against this isolated process.
    var address = $"{context.Request.Scheme}://127.0.0.1:{context.Connection.LocalPort}/health";
    var response = await clients.CreateClient().GetStringAsync(address, context.RequestAborted);
    return Results.Ok(new { result = response });
});
app.MapGet("/fail", () => Fail());
app.Run();
static IResult Fail() => throw new InvalidOperationException("Deliberate demo failure");
