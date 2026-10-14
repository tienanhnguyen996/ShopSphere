# ShopSphere Service Template

Generate a new microservice skeleton for ShopSphere monorepo.

## Installation

Before using the template for the first time, install it locally:

```bash
dotnet new install ./templates/service
```

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

1. **Register projects into the solution**:
   ```bash
   dotnet sln add src/Services/{Name}/{Name}.Api/{Name}.Api.csproj
   dotnet sln add src/Services/{Name}/{Name}.Application/{Name}.Application.csproj
   dotnet sln add src/Services/{Name}/{Name}.Domain/{Name}.Domain.csproj
   dotnet sln add src/Services/{Name}/{Name}.Infrastructure/{Name}.Infrastructure.csproj
   # If withWorker was true:
   dotnet sln add src/Services/{Name}/{Name}.Worker/{Name}.Worker.csproj
   # Tests:
   dotnet sln add tests/{Name}.Tests.Unit/{Name}.Tests.Unit.csproj
   dotnet sln add tests/{Name}.Tests.Integration/{Name}.Tests.Integration.csproj
   ```
2. Update `AGENTS.md` with actual ownership, contracts, state machine
3. Add domain entities to `{Name}.Domain`
4. Implement API controllers in `{Name}.Api/Controllers`
5. Add application commands/queries in `{Name}.Application`
6. Configure EF Core mappings in `{Name}.Infrastructure`
7. Add message consumers in `{Name}.Worker` (if present)
8. Document API in OpenAPI spec
9. Add to Postman collection via Postman MCP

## References

- Root AGENTS.md for architectural rules
- docs/requirements/ for detailed patterns
- Directory.Packages.props for package versions
