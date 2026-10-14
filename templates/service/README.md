# ShopSphere Service Template

Generate a new microservice skeleton for ShopSphere monorepo.

## Usage

From the repository root:

```bash
# Basic service (with Worker, without Saga)
dotnet new shopsphere-service -n Catalog -p 5100

# Service without background worker
dotnet new shopsphere-service -n Notification -p 5106 --withWorker false

# Service with saga support
dotnet new shopsphere-service -n Order -p 5102 --withSaga true
```

## Parameters

- `-n|--name`: Service name (replaces `ServiceName` throughout)
- `-p|--port`: HTTP port (default: 5100)
- `--withWorker`: Include Worker project (default: true)
- `--withSaga`: Include Saga infrastructure (default: false)

## Output Structure

```
src/Services/{Name}/
├── {Name}.Api/
│   ├── Program.cs
│   ├── appsettings.json
│   └── {Name}.Api.csproj
├── {Name}.Application/
│   ├── DependencyInjection.cs
│   └── {Name}.Application.csproj
├── {Name}.Domain/
│   └── {Name}.Domain.csproj
├── {Name}.Infrastructure/
│   ├── DependencyInjection.cs
│   ├── {Name}DbContext.cs
│   └── {Name}.Infrastructure.csproj
├── {Name}.Worker/             # if withWorker=true
│   ├── Program.cs
│   ├── appsettings.json
│   └── {Name}.Worker.csproj
└── AGENTS.md

tests/{Name}.Tests.Unit/
tests/{Name}.Tests.Integration/
```

## What's Included

- **Clean Architecture** layers (Api, Application, Domain, Infrastructure)
- **BuildingBlocks** references (not copied, shared library)
- **EF Core** DbContext with migrations assembly
- **MassTransit** placeholders (Outbox, Consumers)
- **Testcontainers** integration test setup
- **Service-level AGENTS.md** with ownership tables, message contracts, failure rules
- **OpenAPI** spec generation

## Next Steps

After generating:

1. Update `AGENTS.md` with actual ownership, contracts, state machine
2. Add domain entities to `{Name}.Domain`
3. Implement API controllers in `{Name}.Api/Controllers`
4. Add application commands/queries in `{Name}.Application`
5. Configure EF Core mappings in `{Name}.Infrastructure`
6. Add message consumers in `{Name}.Worker` (if present)
7. Document API in OpenAPI spec
8. Add to Postman collection via Postman MCP

## References

- Root AGENTS.md for architectural rules
- docs/requirements/ for detailed patterns
- Directory.Packages.props for package versions
