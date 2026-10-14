# Infrastructure and Deployment

## Local Development Stack

### Docker Compose

For local development, run infrastructure services in Docker:

- **SQL Server 2022**: Shared instance, separate databases per service
- **RabbitMQ 3.13**: Message broker with management UI
- **Redis 7**: Caching layer

See `deploy/compose/docker-compose.yml` for the full configuration.

**Start infrastructure**:
```bash
docker compose -f deploy/compose/docker-compose.yml up -d
```

**Health checks**:
```bash
docker compose -f deploy/compose/docker-compose.yml ps
```

All services should show "healthy" status.

### Database Per Service

Each service owns its database or schema:

- **CatalogService**: `ShopSphere.Catalog`
- **CartService**: `ShopSphere.Cart`
- **OrderService**: `ShopSphere.Orders`
- **InventoryService**: `ShopSphere.Inventory`
- **PaymentService**: `ShopSphere.Payments`
- **ShippingService**: `ShopSphere.Shipping`
- **NotificationService**: `ShopSphere.Notifications`

**Separate migration accounts** per service for production. Dev can share `sa`.

### Connection Strings

Example format:
```
Server=localhost,1433;Database=ShopSphere.Orders;User Id=sa;Password=DevPassword123!;TrustServerCertificate=True
```

Store in:
- `.env` (local, gitignored)
- Azure Key Vault (cloud)
- Kubernetes Secrets (container orchestration)

### Redis Namespacing

Prefix keys by service to avoid conflicts:
- `catalog:product:{sku}`
- `cart:{userId}`
- Never cache: inventory counts, payment state, access rights

## Observability

### OpenTelemetry

Collect logs, metrics, and distributed traces. Export to:
- Console (dev)
- Azure Monitor / Application Insights (Azure)
- Jaeger / Grafana (self-hosted)

**Instrumentation**:
```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSqlClientInstrumentation()
        .AddSource("MassTransit"))
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation());
```

### Key Metrics

- **Checkout duration**: P50, P95, P99
- **Outbox backlog**: Pending messages per service
- **Saga duration**: Time from start to completion
- **Payment timeout rate**: `PaymentUnknown` / total payments
- **Message retry rate**: Per consumer, per message type

### Distributed Tracing

Trace IDs flow through:
1. HTTP request → OrderService
2. Message publish → RabbitMQ
3. Message consume → InventoryService
4. HTTP call → Payment provider

Use `Activity.Current` or OpenTelemetry context propagation.

## Cloud Deployment (Azure)

### Terraform Structure

```
deploy/terraform/
├── modules/
│   ├── app-service/
│   ├── sql-server/
│   ├── service-bus/  # if using Azure Service Bus instead of RabbitMQ
│   └── redis/
├── environments/
│   ├── dev/
│   ├── staging/
│   └── production/
└── shared/
    └── resource-groups.tf
```

### Azure Services

| Component | Azure Service | Notes |
|-----------|--------------|-------|
| APIs | App Service or Container Apps | Linux, .NET 8 runtime |
| Workers | Container Apps with scaling | Consume from RabbitMQ or Service Bus |
| Database | Azure SQL Database | Separate logical servers per env |
| Messaging | RabbitMQ (VM) or Azure Service Bus | Service Bus has native integrations |
| Caching | Azure Cache for Redis | Standard tier minimum |
| Secrets | Key Vault | MSI/Workload Identity for auth |
| Monitoring | Application Insights | OpenTelemetry exporter |

### State Management

Terraform state per environment:
- **Backend**: Azure Storage Account
- **Locking**: Azure Blob lease
- **Encryption**: Customer-managed keys

### Deployment Pipeline

1. **Build**: `dotnet publish` for each service
2. **Test**: Run integration tests with Testcontainers
3. **Package**: Docker images or ZIP deploy
4. **Terraform Plan**: Preview infrastructure changes
5. **Approval Gate**: Manual review (staging, production)
6. **Terraform Apply**: Provision/update resources
7. **Deploy**: Blue-green or rolling update
8. **Smoke Tests**: Health checks + critical path validation

## Security

### Secrets Management

- **Never commit** connection strings, API keys, or certificates
- Use Azure Key Vault references in App Service config
- Rotate secrets regularly (90 days)

### Authentication

- **Service-to-service**: Managed Identity or client credentials
- **User-facing**: OAuth 2.0 / OpenID Connect (Azure AD B2C or external IdP)

### Network Isolation

- Virtual Network integration for App Services
- Private endpoints for SQL, Redis, Key Vault
- Network Security Groups (NSGs) for VM-based RabbitMQ
- No public internet access to databases

## Resilience

### HTTP Clients

Use `Microsoft.Extensions.Http.Resilience`:

```csharp
builder.Services.AddHttpClient<IPaymentClient, PaymentClient>()
    .AddStandardResilienceHandler(options =>
    {
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    });
```

**Do NOT** blindly retry:
- POST requests (may create duplicates)
- Payment charge operations (store attempt ID, query status)

### Database Connections

- Connection pooling (default in EF Core)
- Retry transient failures (EF Core execution strategy)
- Circuit breaker for persistent outages

### Message Processing

- Retry transient errors (MassTransit retry policy)
- Move to error queue after max retries
- Monitor error queue, alert on backlog

## Capacity Planning

### Service Sizing

| Service | CPU | Memory | Notes |
|---------|-----|--------|-------|
| CatalogService | 1 core | 512 MB | Read-heavy, cache-backed |
| CartService | 1 core | 512 MB | Stateless |
| OrderService | 2 cores | 1 GB | Saga state, outbox relay |
| InventoryService | 2 cores | 1 GB | Concurrency control critical |
| PaymentService | 1 core | 512 MB | External API calls |
| ShippingService | 1 core | 512 MB | External API calls |
| NotificationService | 1 core | 512 MB | Async, queue-driven |

Scale out based on:
- RabbitMQ queue depth
- HTTP request rate
- CPU/memory utilization

### Database Sizing

Start with:
- **Dev/Test**: Basic tier (2 vCores, 5 GB)
- **Production**: Standard S2 (50 DTUs) or General Purpose 2 vCores

Monitor:
- Query performance (missing indexes, long-running queries)
- DTU/vCore utilization
- Storage growth

## References

- [Distributed caching in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed)
- [OpenTelemetry in .NET](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-with-otel)
- [HTTP resilience in .NET](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience)
- [Terraform Azure Provider](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs)
