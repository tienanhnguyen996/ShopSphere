# ADR 0003: YARP Reverse Proxy for API Gateway

## Status

Accepted — 2026-10-16

## Context

ShopSphere follows a microservices architecture consisting of independent backend services (Catalog, Cart, Inventory, Payment, Order, Shipping, Notification) and a Single Page Application (SPA) frontend in `src/Web`.

According to the project architecture rules in `AGENTS.md`:
- **Gateway only**: The frontend calls the API gateway/BFF, never individual services directly.
- The gateway must route requests based on path prefixes (e.g., `/api/v1/catalog/**`, `/api/v1/cart/**`, `/api/v1/inventory/**`, `/api/v1/payment/**`).
- When a downstream service is unavailable, the gateway must handle failures gracefully with standard HTTP 503 Bad Gateway / Service Unavailable responses in JSON rather than exposing connection dropouts.
- The gateway must support active/passive health checks, distributed tracing propagation (`traceparent`), and future rate limiting/auth policies without intrusive code changes.

We need to choose the API Gateway / Reverse Proxy technology.

## Decision

**Adopt `Yarp.ReverseProxy` (v2.2.0)** as the API Gateway foundation in `src/Gateway/ShopSphere.Gateway`.

Lock the package version centrally in `Directory.Packages.props`:
```xml
<PackageVersion Include="Yarp.ReverseProxy" Version="2.2.0" />
```

## Rationale

### Advantages of YARP (Yet Another Reverse Proxy)

- ✅ **First-class .NET Core integration**: Built on top of ASP.NET Core `Kestrel` and `SocketsHttpHandler` by Microsoft, offering ultra-high performance, low memory footprint, and native async I/O.
- ✅ **Dynamic and configuration-driven routing**: Supports declarative routes and clusters in `appsettings.json` or code-based configurations with hot-reload capabilities.
- ✅ **Built-in Resilience and Health Checks**: Native support for active and passive destination health checks, automatically detaching unhealthy microservice instances from routing pools.
- ✅ **Seamless OpenTelemetry & Tracing Integration**: Automatically propagates W3C TraceContext headers (`traceparent`, `tracestate`) across HTTP hops to maintain end-to-end distributed transaction traces in OpenTelemetry.
- ✅ **Extensible Middleware Pipeline**: Uses ASP.NET Core middleware, allowing easy addition of custom error handling, header forwarding, rate limiting, and authentication.

### Trade-offs & Mitigations

- **Resource Consumption**: Running a reverse proxy process adds an extra network hop (~1-2ms latency).
  - *Mitigation*: Negligible compared to network traversal; YARP's zero-copy streaming pipeline ensures minimal overhead.
- **Single Point of Failure**: If the gateway is down, all frontend communication is blocked.
  - *Mitigation*: The gateway is completely stateless and horizontally scalable behind Azure Application Gateway / Load Balancer or container replicas.

## Alternatives Considered

### Alternative 1: Ocelot

**Rejected** because:
- Ocelot has slower performance compared to YARP and higher memory allocations.
- Community maintenance has slowed, whereas YARP is actively maintained directly by Microsoft .NET runtime teams.
- Does not leverage modern .NET 8 HTTP transport optimizations as deeply as YARP.

### Alternative 2: Envoy / Traefik / NGINX Sidecars

**Rejected** because:
- Introducing Envoy/NGINX requires additional non-.NET configuration syntax, separate tooling, and operational overhead.
- YARP runs directly on .NET 8, sharing `Directory.Build.props`, `ShopSphere.sln`, OpenTelemetry packages from `ShopSphere.BuildingBlocks`, and standard C# unit/integration testing frameworks (`WebApplicationFactory`).

### Alternative 3: Direct Service Calls from Frontend

**Rejected** because:
- Violates `AGENTS.md` Frontend Rules.
- Exposes internal service topologies, creates CORS complexity, and forces frontend code to manage per-service endpoints.

## References

- [YARP Documentation](https://microsoft.github.io/reverse-proxy/)
- [YARP GitHub Repository](https://github.com/microsoft/reverse-proxy)
- [ShopSphere Architecture Requirements](../../docs/requirements/01-architecture.md)
