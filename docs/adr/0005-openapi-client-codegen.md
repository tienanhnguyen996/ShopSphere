# 5. OpenAPI Client Code Generation for Frontend (openapi-typescript-codegen)

Date: 2026-10-16

## Status

Accepted

## Context

The ShopSphere architecture establishes strict rules for frontend communication:
1. **Gateway Only**: The frontend SPA calls the YARP API Gateway (`/api`), never individual microservices directly.
2. **Generated OpenAPI Client**: TypeScript client code must be automatically generated from backend OpenAPI specs. Hand-written `fetch` or `axios` calls in feature components are strictly prohibited.
3. **Correlation ID & Tracing**: Every outbound HTTP request must carry an `X-Correlation-Id` header for distributed tracing.

To satisfy these requirements, the frontend build pipeline requires a code generation tool capable of consuming the OpenAPI 3.0 specifications (`docs/specs/*-openapi.json`) and generating strongly typed TypeScript models and client services.

## Decision

We adopt `openapi-typescript-codegen` as a development dependency in `src/Web`:

1. **Pure Fetch Client**: Configured with `--client fetch`, generating lightweight, native `fetch` requests with zero external runtime dependencies.
2. **Per-Service Namespacing**: Generate dedicated client modules for each microservice under `src/Web/src/api/generated/<service>`:
   - `catalog`: from `docs/specs/catalog-openapi.json`
   - `cart`: from `docs/specs/cart-openapi.json`
   - `inventory`: from `docs/specs/inventory-openapi.json`
   - `payment`: from `docs/specs/payment-openapi.json`
3. **Automated Generation Script**: Run via `pnpm run generate-client`, which processes all available service OpenAPI specs.
4. **Custom Request Interceptor**: A wrapper in `src/Web/src/api/client.ts` configures `OpenAPI.BASE = '/api'` and intercepts all requests to inject `X-Correlation-Id` (generating a UUID if none exists) and authorization headers when available.

## Consequences

### Positive
- Fully type-safe frontend API calls with compile-time checking against backend contract changes.
- Eliminates manual client code boilerplate and potential typos in API paths or payload schemas.
- Native `fetch` avoids adding large HTTP client libraries (e.g. `axios`) to the production bundle.
- Centralized `X-Correlation-Id` injection ensures 100% trace context propagation across all client requests.

### Negative
- Requires running `pnpm run generate-client` whenever backend OpenAPI specifications change.
- Generated code must be tracked or regenerated during CI build steps.
