using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace NetSpectatr.Tests;

public sealed class DistributionTests
{
    [Fact]
    public void StandardResourceIdentityOverridesFallbacks()
    {
        using var env = new EnvironmentScope(new() {
            ["OTEL_SERVICE_NAME"] = "checkout", ["OTEL_RESOURCE_ATTRIBUTES"] = "service.namespace=shop,k8s.cluster.name=standard-cluster,service.version=2.0",
            ["K8S_CLUSTER_NAME"] = "fallback-cluster", ["K8S_NAMESPACE_NAME"] = "shop", ["K8S_POD_NAME"] = "checkout-123" });
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNetSpectatr(o => o.ServiceName = "fallback");
        using var provider = services.BuildServiceProvider();
        var tracer = provider.GetRequiredService<TracerProvider>();
        var attrs = tracer.GetResource().Attributes.ToDictionary(x => x.Key, x => x.Value);
        Assert.Equal("checkout", attrs["service.name"]);
        Assert.Equal("standard-cluster", attrs["k8s.cluster.name"]);
        Assert.Equal("checkout-123", attrs["k8s.pod.name"]);
        Assert.Equal("shop", attrs["service.namespace"]);
        Assert.Equal("2.0", attrs["service.version"]);
        Assert.DoesNotContain(attrs.Keys, x => x.StartsWith("beholdr."));
    }

    [Fact]
    public void ParentSamplingDecisionSurvivesZeroRootSampling()
    {
        using var env = new EnvironmentScope(new() { ["OTEL_TRACES_SAMPLER"] = null });
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNetSpectatr(o => { o.ServiceName = "test"; o.SamplingRatio = 0; o.ActivitySources = ["test-source"]; });
        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<TracerProvider>();
        using var source = new ActivitySource("test-source");
        using var root = source.StartActivity("root");
        Assert.False(root?.Recorded ?? false);
        var parent = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, isRemote: true);
        using var child = source.StartActivity("child", ActivityKind.Server, parent);
        Assert.True(child?.Recorded);
    }

    [Fact]
    public void StandardSamplerEnvironmentWins()
    {
        using var env = new EnvironmentScope(new() { ["OTEL_TRACES_SAMPLER"] = "always_off" });
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNetSpectatr(o => { o.ServiceName = "test"; o.SamplingRatio = 1; o.ActivitySources = ["env-source"]; });
        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<TracerProvider>();
        using var source = new ActivitySource("env-source");
        using var activity = source.StartActivity("operation");
        Assert.False(activity?.Recorded ?? false);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExceptionDetailsAreExplicitlyOptIn(bool details)
    {
        using var activity = new Activity("failed-request").Start();
        NetSpectatrExtensions.EnrichException(activity, new InvalidOperationException("sensitive detail"), details);
        var tags = Assert.Single(activity.Events).Tags.ToDictionary(x => x.Key, x => x.Value);
        Assert.Equal(typeof(InvalidOperationException).FullName, tags["exception.type"]);
        Assert.Equal(details, tags.ContainsKey("exception.message"));
        Assert.Equal(details, tags.ContainsKey("exception.stacktrace"));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void InvalidSamplingFailsAtStartup(double ratio)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ServiceCollection().AddNetSpectatr(o => o.SamplingRatio = ratio));
    }

    private sealed class EnvironmentScope : IDisposable
    {
        private readonly Dictionary<string, string?> previous = new();
        public EnvironmentScope(Dictionary<string, string?> values)
        {
            foreach (var (key, value) in values) { previous[key] = Environment.GetEnvironmentVariable(key); Environment.SetEnvironmentVariable(key, value); }
        }
        public void Dispose() { foreach (var (key, value) in previous) Environment.SetEnvironmentVariable(key, value); }
    }
}
