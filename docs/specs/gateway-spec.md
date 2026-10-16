# API Gateway Specification

## 1. Overview & Gateway Boundaries

The ShopSphere API Gateway (`ShopSphere.Gateway`) serves as the single reverse proxy ingress point for client applications (such as the React SPA in `src/Web`), routing requests to internal backend microservices.

- **Boundary**: Ingress reverse proxy and request dispatcher. The Gateway owns zero business domain entities and has no direct database access.
- **Technology**: Built on ASP.NET Core 8.0 and `Yarp.ReverseProxy` (v2.2.0).
- **Security & Headers**:
  - Automatically preserves and forwards W3C TraceContext headers (`traceparent`, `tracestate`).
  - Generates or forwards `X-Correlation-Id` for cross-service transaction tracking.
  - Adds `X-Forwarded-For`, `X-Forwarded-Proto`, and `X-Forwarded-Host` headers to downstream requests.
- **Resilience & Graceful Failures**:
  - In the event of a downstream service outage, network partition, or destination timeout, the Gateway catches proxy exceptions and returns a structured JSON `503 Service Unavailable` error instead of dropping the TCP connection.

```
+-----------------------------------------------------------------------------------+
|                           Frontend Client (src/Web)                               |
+-----------------------------------------------------------------------------------+
                                    |
                                    | HTTP /api/v1/{route}
                                    v
+-----------------------------------------------------------------------------------+
|                      ShopSphere API Gateway (YARP Reverse Proxy)                  |
|  - W3C TraceContext Propagation (OpenTelemetry)                                   |
|  - Dynamic Route & Cluster Dispatching                                            |
|  - Graceful 503 Service Unavailable Error Handling Middleware                     |
+-----------------------------------------------------------------------------------+
          |                     |                    |                   |
          | /api/v1/catalog/**  | /api/v1/cart/**    | /api/v1/inventory | /api/v1/payment
          v                     v                    v                   v
+------------------+  +------------------+  +------------------+  +-----------------+
| Catalog Service  |  |   Cart Service   |  | Inventory Service|  | Payment Service |
| (port 5001)      |  |   (port 5002)    |  | (port 5003)      |  | (port 5004)     |
+------------------+  +------------------+  +------------------+  +-----------------+
```

---

## 2. Reverse Proxy Route & Cluster Configuration

Routes and clusters are defined in `appsettings.json` under the `ReverseProxy` configuration section:

### 2.1 Route Definitions

| Route ID | Path Match Pattern | Target Cluster | Path Transforms |
| :--- | :--- | :--- | :--- |
| `catalog-route` | `/api/v1/catalog/{**catch-all}` | `catalog-cluster` | None (preserves path) |
| `catalog-products-route` | `/api/v1/products/{**catch-all}` | `catalog-cluster` | None (preserves route alias) |
| `cart-route` | `/api/v1/cart/{**catch-all}` | `cart-cluster` | None |
| `inventory-route` | `/api/v1/inventory/{**catch-all}` | `inventory-cluster` | None |
| `payment-route` | `/api/v1/payment/{**catch-all}` | `payment-cluster` | None |
| `orders-route` | `/api/v1/orders/{**catch-all}` | `order-cluster` | None |
| `shipping-route` | `/api/v1/shipping/{**catch-all}` | `shipping-cluster` | None |

### 2.2 Cluster Definitions

| Cluster ID | Destination Address (Default / Dev) | Active Health Check Path | Interval |
| :--- | :--- | :--- | :--- |
| `catalog-cluster` | `http://localhost:5001` | `/health` | 15s |
| `cart-cluster` | `http://localhost:5002` | `/health` | 15s |
| `inventory-cluster` | `http://localhost:5003` | `/health` | 15s |
| `payment-cluster` | `http://localhost:5004` | `/health` | 15s |
| `order-cluster` | `http://localhost:5005` | `/health` | 15s |
| `shipping-cluster` | `http://localhost:5006` | `/health` | 15s |

---

## 3. Failure Handling & JSON Error Contract

When a downstream destination fails or times out:
1. YARP sets `IForwarderErrorFeature` on `HttpContext.Features`.
2. A custom Gateway middleware intercepts `ForwarderError` (`NoAvailableDestinations`, `RequestTimedOut`, `ServiceUnavailable`).
3. The response status code is set to `503 Service Unavailable` with `Content-Type: application/json`.

### Error Response Schema:
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.4",
  "title": "Service Unavailable",
  "status": 503,
  "detail": "Downstream service is temporarily unavailable or unreachable.",
  "code": "Gateway.ServiceUnavailable",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
}
```

---

## 4. Health Checks & Observability

- **Gateway Health Check**: Endpoint `GET /health` returns `200 OK` with JSON `{ "status": "Healthy" }`.
- **OpenTelemetry**: Integrated using `ShopSphere.BuildingBlocks.Observability`, registering OpenTelemetry metrics and tracing with service name `ShopSphere.Gateway`.
- **CORS Policy**: Configured to allow requests from the frontend SPA (`http://localhost:3000` / `http://localhost:5173`) with credentials, methods (`GET`, `POST`, `PUT`, `DELETE`, `OPTIONS`), and headers.
