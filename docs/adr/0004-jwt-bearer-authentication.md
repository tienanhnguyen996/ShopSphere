# ADR 0004: Microsoft.AspNetCore.Authentication.JwtBearer for Distributed Authentication

## Status

Accepted — 2026-10-16

## Context

ShopSphere is structured as an event-driven microservices architecture with multiple backend services (`Catalog`, `Cart`, `Inventory`, `Payment`, `Identity`) and an API Gateway (`Gateway.Host`).

According to `AGENTS.md` and `docs/superpowers/specs/2026-10-16-identity-auth-service-design.md`:
- Each service enforces its own security perimeter independently.
- Avoid remote HTTP calls back to Identity on every incoming request ("Never perform remote HTTP calls from downstream services back to Identity on every request; authorization claims are packed into verifiable tokens").
- Services and the API Gateway must validate signed JSON Web Tokens (JWTs) locally using symmetric secret keys (`HS256`).
- Standard ASP.NET Core policy-based authorization (`RequireClaim("permission", ...)`) must be used across all endpoints.

We need a standardized, battle-tested JWT bearer authentication library for ASP.NET Core in `ShopSphere.BuildingBlocks`.

## Decision

**Adopt `Microsoft.AspNetCore.Authentication.JwtBearer` (v8.0.10)** across `ShopSphere.BuildingBlocks` and consuming microservices.

Manage version centrally in `Directory.Packages.props`:
```xml
<PackageVersion Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.10" />
```

## Rationale

### Advantages

- ✅ **Official Microsoft ASP.NET Core Security Component**: Directly maintained by the .NET runtime team with zero third-party vulnerability risk.
- ✅ **High Performance Zero-Hop Validation**: Performs cryptographic signature verification (`HS256`/`HMAC-SHA256`), issuer/audience validation, and lifetime checks locally in memory in microseconds.
- ✅ **Native Integration with ASP.NET Core Middleware**: Seamlessly registers with `IServiceCollection.AddAuthentication().AddJwtBearer()`, populating `ClaimsPrincipal` (`HttpContext.User`) for downstream authorization filters and endpoint handlers.
- ✅ **Centralized Configuration via BuildingBlocks**: Encapsulated in `AddShopSphereAuthentication(IConfiguration)` extension method so individual services do not duplicate token validation boilerplate.

### Trade-offs & Mitigations

- **Secret Key Synchronization**: Microservices must share or receive the same token signing key.
  - *Mitigation*: Shared via environment variables / configuration (`Jwt:SecretKey`), with fallback defaults for local development and unit tests.

## Alternatives Considered

### Alternative 1: Custom Token Validation Middleware

**Rejected** because:
- High security risk of introducing crypto and padding flaws.
- Unnecessary reinvention of standard RFC 7519 JWT validation.

### Alternative 2: IdentityModel / Duende IdentityServer Client

**Rejected** because:
- ShopSphere uses an in-house Identity microservice issuing signed JWTs; heavy OAuth2 introspection protocols are unnecessary and introduce runtime remote HTTP dependencies.

## References

- [Microsoft.AspNetCore.Authentication.JwtBearer Documentation](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/)
- [ShopSphere Identity Service Specification](../superpowers/specs/2026-10-16-identity-auth-service-design.md)
