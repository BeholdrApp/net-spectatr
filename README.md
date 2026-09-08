# net-spectatr

**Beholdr's OpenTelemetry distribution for .NET applications.**

`net-spectatr` instruments .NET applications for
[Beholdr](https://github.com/BeholdrApp/Beholdr): traces, metrics, exceptions
and runtime/application telemetry, exported over OTLP.

## What it is — and what it deliberately is not

It is a **distribution of OpenTelemetry .NET**, not an agent written from
scratch. Upstream already solves auto-instrumentation for ASP.NET Core,
HttpClient, EF Core, and the .NET runtime, and reimplementing that would mean
inheriting its maintenance burden forever without the benefit of its testing.

What `net-spectatr` adds on top:

- Opinionated defaults so a service is usefully instrumented with one package
  reference and no bespoke wiring.
- Beholdr resource attributes populated from the environment, so telemetry
  joins correctly to clusters, namespaces and workloads.
- A configuration surface that matches how Beholdr is deployed.
- Exception and runtime telemetry enabled and shaped consistently across
  services.

## The contract is OTLP

Beholdr ingests **standard OTLP with standard semantic conventions**. Nothing
in the platform requires this package. A service already exporting OTLP — from
the upstream OpenTelemetry .NET SDK, the OpenTelemetry Java agent, or any other
compliant instrumentation — is a first-class citizen.

`net-spectatr` is a convenience, not a dependency. That is the point: adding a
new language to a Beholdr deployment must not require writing a new agent.

## Status

Early. See the [issues](https://github.com/BeholdrApp/net-spectatr/issues) and
the [Beholdr roadmap](https://github.com/BeholdrApp/Beholdr/blob/main/ROADMAP.md).

## License

All rights reserved. See [LICENSE](LICENSE).
