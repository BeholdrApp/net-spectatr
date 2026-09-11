using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace NetSpectatr;

public static class NetSpectatrExtensions
{
    /// <summary>Configure upstream tracing, metrics and bounded OTLP exporters.</summary>
    public static OpenTelemetryBuilder AddNetSpectatr(this IServiceCollection services, Action<NetSpectatrOptions>? configure = null)
    {
        var options = new NetSpectatrOptions();
        configure?.Invoke(options);
        if (!double.IsFinite(options.SamplingRatio) || options.SamplingRatio is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(options.SamplingRatio));

        var entry = Assembly.GetEntryAssembly()?.GetName();
        var fallback = options.ServiceName ?? entry?.Name ?? "unknown_service:dotnet";
        var attributes = ResourceAttributes(options);
        var builder = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(fallback, serviceVersion: options.ServiceVersion ?? entry?.Version?.ToString(), serviceNamespace: options.ServiceNamespace)
                .AddAttributes(attributes)
                // Standard OTEL_RESOURCE_ATTRIBUTES / OTEL_SERVICE_NAME override fallbacks.
                .AddEnvironmentVariableDetector())
            .WithTracing(tracing =>
            {
                // Leave upstream's environment sampler selection intact when configured.
                if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OTEL_TRACES_SAMPLER")))
                    tracing.SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(options.SamplingRatio)));
                tracing.AddAspNetCoreInstrumentation(o =>
                    o.EnrichWithException = (activity, exception) => EnrichException(activity, exception, options.CaptureExceptionDetails));
                tracing.AddHttpClientInstrumentation(o =>
                    o.EnrichWithException = (activity, exception) => EnrichException(activity, exception, options.CaptureExceptionDetails));
                if (options.EnableEntityFrameworkCore) tracing.AddEntityFrameworkCoreInstrumentation();
                if (options.EnableSqlClient) tracing.AddSqlClientInstrumentation();
                tracing.AddSource(options.ActivitySources);
                tracing.AddOtlpExporter(); // Upstream batch processor: bounded, asynchronous.
            })
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(options.Meters)
                .AddOtlpExporter());

        if (options.ServiceName is null && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME")) &&
            !(Environment.GetEnvironmentVariable("OTEL_RESOURCE_ATTRIBUTES") ?? "").Split(',').Any(x => x.Trim().StartsWith("service.name=", StringComparison.Ordinal)))
            services.AddSingleton<IHostedService>(sp => new IdentityDiagnostic(sp.GetRequiredService<ILogger<IdentityDiagnostic>>(), fallback));
        return builder;
    }

    internal static Dictionary<string, object> ResourceAttributes(NetSpectatrOptions options)
    {
        var result = new Dictionary<string, object>();
        foreach (var (environment, attribute) in new[] {
            ("K8S_NAMESPACE_NAME", "k8s.namespace.name"), ("K8S_POD_NAME", "k8s.pod.name"),
            ("K8S_POD_UID", "k8s.pod.uid"), ("K8S_CONTAINER_NAME", "k8s.container.name"),
            ("K8S_DEPLOYMENT_NAME", "k8s.deployment.name"), ("K8S_CLUSTER_NAME", "k8s.cluster.name"),
            ("DOTNET_ENVIRONMENT", "deployment.environment.name") })
        {
            var value = Environment.GetEnvironmentVariable(environment);
            if (!string.IsNullOrWhiteSpace(value)) result[attribute] = value;
        }
        return result;
    }

    internal static void EnrichException(Activity activity, Exception exception, bool details)
    {
        var tags = new ActivityTagsCollection { ["exception.type"] = exception.GetType().FullName };
        activity.SetTag("error.type", exception.GetType().FullName);
        if (details)
        {
            tags["exception.message"] = exception.Message;
            tags["exception.stacktrace"] = exception.ToString();
        }
        activity.AddEvent(new ActivityEvent("exception", tags: tags));
    }

    private sealed class IdentityDiagnostic(ILogger<IdentityDiagnostic> logger, string fallback) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogWarning("NetSpectatr: OTEL_SERVICE_NAME is unset; using {ServiceName}. Set a stable service name to correlate telemetry.", fallback);
            return Task.CompletedTask;
        }
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
