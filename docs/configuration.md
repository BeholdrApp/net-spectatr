# Configuration and support

Preview targets .NET 8 and .NET 10; CI builds and tests both. Support follows
Microsoft's support window for each target. This is not a commitment to extend
.NET 8 beyond its upstream end of support.

```csharp
using NetSpectatr;
builder.Services.AddNetSpectatr(options => {
    options.ServiceName = "checkout";
    options.ServiceNamespace = "shop";
    options.ServiceVersion = "1.0.0";
    options.ActivitySources = ["Checkout.Orders"];
    options.Meters = ["Checkout.Orders"];
});
```

ASP.NET Core, HttpClient, SqlClient and runtime instrumentation come from
upstream OpenTelemetry 1.18.0. EF Core is upstream 1.18.0-beta.1 and explicitly
opt-in with `EnableEntityFrameworkCore = true`; avoid enabling duplicate
instrumentation on an already instrumented service. SQL statement/parameter
capture is not enabled by this distribution. Upstream dependency versions and
transitive graphs are committed in project files and NuGet lock files. Update
the release family together, review upstream release notes, regenerate locks,
and run framework tests plus Beholdr's agents conformance check.

Standard `OTEL_EXPORTER_OTLP_*` settings configure endpoints, protocol, headers,
timeouts and TLS. OTLP exporter defaults remain upstream defaults (gRPC on
localhost:4317). For HTTP, set `OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf` and a
base `OTEL_EXPORTER_OTLP_ENDPOINT=http://127.0.0.1:4318`. Signal-specific
endpoints include `/v1/traces` or `/v1/metrics`.

`OTEL_SERVICE_NAME` and `OTEL_RESOURCE_ATTRIBUTES` override fallback identity.
Without an explicit name the entry assembly name is used and a startup warning
asks for a stable name. Optional Kubernetes identity is mapped from:

| Environment | Resource attribute |
| --- | --- |
| K8S_NAMESPACE_NAME | k8s.namespace.name |
| K8S_POD_NAME | k8s.pod.name |
| K8S_POD_UID | k8s.pod.uid |
| K8S_CONTAINER_NAME | k8s.container.name |
| K8S_DEPLOYMENT_NAME | k8s.deployment.name |
| K8S_CLUSTER_NAME | k8s.cluster.name |
| DOTNET_ENVIRONMENT | deployment.environment.name |

Use Downward API field references for namespace, pod name and UID. Deployment,
container and cluster names must come from deployment configuration; Kubernetes
does not expose a direct Downward API field for every attribute. Missing
Kubernetes identity is valid outside Kubernetes.

Root traces default to 10% sampling with parent decisions honored. A configured
`OTEL_TRACES_SAMPLER` takes precedence. Batch export uses upstream's bounded
2048-span queue, 512-span batches and asynchronous processing; normal request
execution does not wait for export. Set standard `OTEL_BSP_*` variables to tune
the queue. Metrics use upstream periodic export and SDK cardinality limits.
When queues fill, telemetry is dropped rather than buffering without a limit.

Upstream exception callbacks attach typed exception events. Message and stack
trace are excluded unless `CaptureExceptionDetails = true`. Runtime exception
counts remain enabled. Head sampling can drop error traces. Process-fatal
exceptions cannot be guaranteed to flush before termination; this package does
not install a blocking process-wide crash hook. Application logging providers
and other instrumentation retain their own data-capture policies.

## Build and packaging

```sh
dotnet restore --locked-mode
dotnet test -c Release
dotnet pack src/NetSpectatr -c Release -o out/packages
```

CI produces deterministic NuGet/symbol artifacts with SourceLink and audits
transitive vulnerabilities during restore. NuGet.org publishing, package
signing, a release SBOM and automated license policy remain open work; no
credentials or release destination are assumed. The locally built preview can
be consumed from a local NuGet source. The sample uses a project reference.

`samples/StockOtelApi` has no NetSpectatr reference and is the conformance control.
Both samples work with any standard OTLP Collector. A Beholdr package or private
resource attribute is never required by the receiving endpoint.
