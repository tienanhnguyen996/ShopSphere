# YARP API Gateway Scaffolding & Routing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the ShopSphere API Gateway using YARP reverse proxy to route frontend client requests to internal microservices with resilience, health checks, and OpenTelemetry observability.

**Architecture:** ASP.NET Core 8.0 Web API host utilizing `Yarp.ReverseProxy` with declarative route-to-cluster mappings for all services (Catalog, Cart, Inventory, Payment, Orders, Shipping). Incorporates custom error-handling middleware that catches downstream unreachable exceptions to return standard JSON 503 Service Unavailable problem details. Observability is wired through `ShopSphere.BuildingBlocks.Observability`.

**Tech Stack:** .NET 8.0, `Yarp.ReverseProxy` 2.2.0, OpenTelemetry, xUnit, FluentAssertions, `Microsoft.AspNetCore.Mvc.Testing`.

**Spec:** [`docs/specs/gateway-spec.md`](../../specs/gateway-spec.md) and [`docs/adr/0003-yarp-reverse-proxy.md`](../../adr/0003-yarp-reverse-proxy.md).

## Global Constraints

- Must target .NET 8.0 (`net8.0`) and respect central package management in `Directory.Packages.props`.
- Must follow Conventional Commits with Notion Task ID: `feat(gateway): [Task-10] <description>`.
- Strict zero-warning build (`TreatWarningsAsErrors: true` via `Directory.Build.props`).
- Zero direct database access in Gateway — pure reverse proxy and protocol mediation.
- All code must pass style and formatting checks via `scripts/lint.ps1`.

## Review Focus

1. **Downstream Unreachable / Connection Refused**: Returns HTTP 503 with JSON problem contract, not an empty or dropped TCP socket.
2. **Missing Route / 404 Handling**: Unmatched request paths fall through cleanly to standard 404 Not Found.
3. **TraceContext Propagation**: W3C `traceparent` headers received from clients are preserved and forwarded across backend cluster calls.
4. **CORS Policy for Frontend SPA**: Allows `localhost:3000` and `localhost:5173` with credentials and standard HTTP verbs.
5. **Gateway Health Check**: `/health` responds with HTTP 200 OK without depending on downstream microservice availability.

---

### Task 1: Package Configuration & Project Scaffolding

**Files:**
- Modify: `Directory.Packages.props`
- Create: `src/Gateway/ShopSphere.Gateway/ShopSphere.Gateway.csproj`
- Create: `tests/Gateway.Tests.Unit/Gateway.Tests.Unit.csproj`
- Modify: `ShopSphere.sln`

**Interfaces:**
- Produces: `ShopSphere.Gateway` runnable project and `Gateway.Tests.Unit` test project linked in solution.

- [ ] **Step 1: Write test project file and initial test**

```csharp
namespace ShopSphere.Gateway.Tests.Unit;

public class GatewayScaffoldingTests
{
    [Fact]
    public void GatewayAssembly_ShouldBeAvailable()
    {
        var assembly = typeof(Program).Assembly;
        assembly.Should().NotBeNull();
    }
}
```

- [ ] **Step 2: Add `Yarp.ReverseProxy` to `Directory.Packages.props`**

Add `<PackageVersion Include="Yarp.ReverseProxy" Version="2.2.0" />` under a new or existing ItemGroup in `Directory.Packages.props`.

- [ ] **Step 3: Create `src/Gateway/ShopSphere.Gateway/ShopSphere.Gateway.csproj`**

Target `net8.0`, include project reference to `src/BuildingBlocks/BuildingBlocks.csproj`, and package reference to `Yarp.ReverseProxy`.

- [ ] **Step 4: Create `tests/Gateway.Tests.Unit/Gateway.Tests.Unit.csproj` and add both to `ShopSphere.sln`**

Include references to `ShopSphere.Gateway.csproj`, `Microsoft.AspNetCore.Mvc.Testing`, `FluentAssertions`, and `xunit`.

- [ ] **Step 5: Verify build**

Run build command to verify project references and solution structure compile cleanly.

- [ ] **Step 6: Commit**

```bash
git add Directory.Packages.props src/Gateway/ tests/Gateway.Tests.Unit/ ShopSphere.sln
git commit -m "infra(gateway): [Task-10] scaffold YARP reverse proxy and test projects"
```

---

### Task 2: Reverse Proxy Configuration & Custom Error Handling Middleware

**Files:**
- Create: `src/Gateway/ShopSphere.Gateway/appsettings.json`
- Create: `src/Gateway/ShopSphere.Gateway/Middleware/GatewayErrorHandlingMiddleware.cs`
- Create: `tests/Gateway.Tests.Unit/GatewayConfigurationTests.cs`

**Interfaces:**
- Produces: `GatewayErrorHandlingMiddleware` mapping `IForwarderErrorFeature` to standard 503 JSON problem response.
- Produces: `ReverseProxy` configuration section defining routes and clusters for Catalog, Cart, Inventory, Payment, Orders, Shipping.

- [ ] **Step 1: Write failing tests for configuration and error middleware**

```csharp
[Fact]
public void ReverseProxyConfiguration_ShouldContainAllMicroserviceRoutes()
{
    // Verifies routes for catalog, cart, inventory, payment, orders, shipping
}

[Fact]
public async Task GatewayErrorMiddleware_WhenForwarderErrorPresent_ShouldReturn503Json()
{
    // Verifies 503 response with Gateway.ServiceUnavailable code
}
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Implement `GatewayErrorHandlingMiddleware` and `appsettings.json`**

Implement middleware checking `context.Features.Get<IForwarderErrorFeature>()` and writing RFC 7807 JSON with `Gateway.ServiceUnavailable`. Define routes and clusters in `appsettings.json`.

- [ ] **Step 4: Run tests to verify they pass**

- [ ] **Step 5: Commit**

```bash
git add src/Gateway/ShopSphere.Gateway/ tests/Gateway.Tests.Unit/
git commit -m "feat(gateway): [Task-10] configure reverse proxy routes and failure handling middleware"
```

---

### Task 3: Host Pipeline, CORS, Health Checks & Integration Verification

**Files:**
- Create: `src/Gateway/ShopSphere.Gateway/Program.cs`
- Create: `tests/Gateway.Tests.Unit/GatewayApiTests.cs`

**Interfaces:**
- Produces: Fully functional `Program` entry point mounting `MapReverseProxy`, `UseCors`, `AddShopSphereObservability`, `/health`, and error middleware.

- [ ] **Step 1: Write failing integration test for Gateway `/health` and route dispatching**

```csharp
[Fact]
public async Task Gateway_HealthEndpoint_ShouldReturn200Healthy()
{
    var response = await _client.GetAsync("/health");
    response.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Implement `Program.cs` with full middleware pipeline**

Configure `AddReverseProxy().LoadFromConfig(...)`, CORS policy, OpenTelemetry, `/health` endpoint, and custom middleware.

- [ ] **Step 4: Run all gateway tests to verify they pass**

- [ ] **Step 5: Commit**

```bash
git add src/Gateway/ShopSphere.Gateway/ tests/Gateway.Tests.Unit/
git commit -m "feat(gateway): [Task-10] implement gateway host pipeline and health check endpoints"
```

---

### Task 4: CI Workflow Update & Final Verification

**Files:**
- Modify: `.github/workflows/test.yml`

- [ ] **Step 1: Add `Gateway.Tests.Unit` to `.github/workflows/test.yml`**

Add test runner execution step for `tests/Gateway.Tests.Unit/Gateway.Tests.Unit.csproj` so it runs in CI.

- [ ] **Step 2: Run pre-commit style and lint checks**

Run `powershell -ExecutionPolicy Bypass -File scripts\lint.ps1`.

- [ ] **Step 3: Commit and push**

```bash
git add .github/workflows/test.yml
git commit -m "ci(gateway): [Task-10] add gateway unit tests to CI workflow"
```
