# ShopSphere

AI-powered e-commerce fulfillment platform built with .NET microservices. Orchestrates catalog, cart, inventory, payment, and shipping through resilient workflows with saga orchestration and transactional messaging.

## Quick Start (5 Minutes)

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop) running
- [Visual Studio Code](https://code.visualstudio.com/) with [Dev Containers extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers)
- [Git](https://git-scm.com/)

### Setup

1. **Clone the repository**:
   ```bash
   git clone <repository-url>
   cd ShopSphere
   ```

2. **Create your environment file**:
   ```bash
   cp .env.example .env
   ```

3. **Add your tokens** (see [Token Creation](#token-creation) below):
   ```bash
   # Edit .env
   GITHUB_TOKEN=ghp_YOUR_TOKEN_HERE
   NOTION_TOKEN=secret_YOUR_TOKEN_HERE
   ```

4. **Open in Dev Container**:
   - Open folder in VS Code
   - When prompted, click "Reopen in Container"
   - Or: `Ctrl+Shift+P` → "Dev Containers: Reopen in Container"
   - Wait for build + bootstrap (2-3 minutes first time)

5. **Verify environment**:
   ```bash
   bash scripts/doctor.sh
   ```
   Should show all ✅ checks passing.

6. **Start infrastructure**:
   ```bash
   docker compose -f deploy/compose/docker-compose.yml up -d
   ```
   Wait ~30 seconds for health checks.

7. **Verify infrastructure**:
   ```bash
   docker compose -f deploy/compose/docker-compose.yml ps
   ```
   All services should show "healthy" status.

**You're ready!** Core services are implemented: Identity (authentication, JWT, refresh tokens, role/permission authorization), Catalog (products, prices, SKU validation), Cart (Redis persistence, price validation), Inventory (stock reservations, holds), Payment (transactional outbox, mock provider), and YARP API Gateway. Follow the development workflow below for running and extending services.

## One-Time Manual Steps

These require human interaction and cannot be automated:

### GitHub Authentication

For GitHub MCP server (PR/issue management):

```bash
gh auth login
```

Follow prompts to authenticate via browser or token.

### Notion Integration

For Notion MCP server (docs/specs):

1. Go to https://www.notion.so/my-integrations
2. Click "+ New integration"
3. Name: "ShopSphere Docs"
4. Associated workspace: Select your workspace
5. Capabilities: Read content, Update content, Insert content
6. Submit → Copy "Internal Integration Secret" (starts with `secret_`)
7. Add to `.env`: `NOTION_TOKEN=secret_...`
8. **Important**: Go to Notion pages/databases you want accessible, click **Share**, invite the integration by name

## Token Creation

### GitHub Fine-Grained Personal Access Token

Use a fine-grained personal access token with least-privilege permissions scoped only to this repository:

1. Visit https://github.com/settings/personal-access-tokens/new
2. Token name: "ShopSphere AI Agent"
3. Expiration: 90 days (rotate regularly)
4. Repository access: **Only select repositories** → Select `ShopSphere`
5. Repository permissions:
   - ✅ **Contents**: Read and write (for code inspection and branch commits)
   - ✅ **Pull requests**: Read and write (for PR creation and review)
   - ✅ **Issues**: Read and write (for task tracking)
   *(Note: `workflow` permission is dropped for security unless managing CI workflows)*
6. Generate token → Copy (starts with `github_pat_` or classic `ghp_`)
7. Add to `.env`: `GITHUB_TOKEN=github_pat_...`

### Notion Integration Token

See [Notion Integration](#notion-integration) above.

## Architecture

ShopSphere is a microservices e-commerce platform with:

- **7 Services**: Identity, Catalog, Cart, Order, Inventory, Payment, Shipping
- **Saga Orchestration**: Checkout workflow coordinates stock reservation → payment → shipping
- **Transactional Outbox**: Atomic writes + messaging, no lost/duplicate messages
- **Eventual Consistency**: Services update asynchronously, expose intermediate states
- **Resilience Patterns**: Retries, circuit breakers, timeout = unknown (not failure)

**Key Principles**:
- Each service owns its data (no cross-service DB access)
- Redis caches reads only (not source of truth)
- Concurrency control via EF Core tokens
- Late events check state/version before applying

See [AGENTS.md](./AGENTS.md) for complete architectural rules.

## Project Structure

```
ShopSphere/
├── src/
│   ├── BuildingBlocks/      # Shared cross-cutting concerns (Security, Results, Outbox)
│   ├── Gateway/             # YARP reverse proxy API gateway
│   ├── Services/            # Microservices (Identity, Catalog, Cart, Inventory, Payment, etc.)
│   └── Web/                 # React SPA
├── tests/                   # Unit and integration test suites
├── deploy/
│   ├── compose/             # Local Docker infra (SQL Server, RabbitMQ, Redis)
│   └── terraform/           # IaC modules
├── docs/
│   ├── requirements/        # Architecture, services, messaging, infra
│   ├── adr/                 # Architecture Decision Records
│   ├── specs/               # Service OpenAPI specifications
│   └── agent-setup.md       # MCP configuration guide
├── scripts/
│   ├── bootstrap.sh         # First-run setup
│   ├── doctor.sh / .ps1     # Environment health checks
│   └── lint.sh / .ps1       # Code formatting and lint verification
└── AGENTS.md                # Architecture + workflow rules (<150 lines)
```

## Technology Stack

| Layer | Technology |
|-------|-----------|
| **Backend** | .NET 10 LTS, ASP.NET Core, EF Core |
| **Frontend** | React 18, TypeScript, Vite, YARP gateway |
| **Messaging** | MassTransit 8.3.3, RabbitMQ 3.13 |
| **Data** | SQL Server 2022, Redis 7 |
| **Testing** | xUnit, FluentAssertions, Testcontainers |
| **Observability** | OpenTelemetry, Jaeger, Grafana (local) |
| **IaC** | Terraform |

## Development Workflow

1. **Check requirements**: Review `docs/requirements/` for patterns
2. **Write spec** (if new feature): API, messages, state transitions
3. **Implement**: Follow service boundaries, use Transactional Outbox
4. **Test**: Unit + integration, **include failure paths**
5. **Run doctor**: `bash scripts/doctor.sh` (or `pwsh scripts/doctor.ps1`) before committing
6. **Commit**: Use [Conventional Commits](https://www.conventionalcommits.org/)
7. **PR**: Use template checklist (architecture, testing, verification)

## Agent Support

AI agents configured via MCP (Model Context Protocol):

- **Claude Code**: Auto-configured via `.mcp.json`
- **Antigravity**: Centralized MCP configuration (see [docs/agent-setup.md](./docs/agent-setup.md))

All agents have access to:
- GitHub PR/issue management
- Notion docs/specs
- Terraform inspection (read-only)
- Codebase context indexing
- Microsoft Learn docs

## MCP Servers

| Server | Purpose |
|--------|---------|
| **github** | PR creation, issue management, repo operations |
| **notion** | Docs/specs management |
| **context7** | Codebase context indexing |
| **microsoft-learn** | .NET/Azure documentation |
| **terraform** | Infrastructure inspection (read-only) |

## Useful Commands

```bash
# Health check
bash scripts/doctor.sh

# Start infrastructure
docker compose -f deploy/compose/docker-compose.yml up -d

# Check infrastructure status
docker compose -f deploy/compose/docker-compose.yml ps

# Stop infrastructure
docker compose -f deploy/compose/docker-compose.yml down

# View logs
docker compose -f deploy/compose/docker-compose.yml logs -f [service]

# Format C# code
dotnet format

# Run tests (when implemented)
dotnet test

# Terraform plan (dev environment)
cd deploy/terraform/environments/dev
terraform plan
```

## Troubleshooting

### Dev container fails to build

```bash
# Rebuild without cache
Ctrl+Shift+P → "Dev Containers: Rebuild Container"
```

### Docker Compose health checks fail

```bash
# Check container logs
docker compose -f deploy/compose/docker-compose.yml logs [sqlserver|rabbitmq|redis]

# Restart infrastructure
docker compose -f deploy/compose/docker-compose.yml restart
```

### MCP servers not working

1. Check tokens in `.env`
2. Restart IDE
3. Verify MCP config syntax: `cat .mcp.json | jq`
4. See detailed troubleshooting in [docs/agent-setup.md](./docs/agent-setup.md#troubleshooting)

### Skills not found

#### Symlink Method (Recommended)

```bash
# Git Bash / WSL
ln -s ../.agents/skills .claude/skills

# PowerShell (requires Developer Mode or admin)
cmd /c mklink /D .claude\skills .agents\skills
```

#### Fallback: Sync Script (Windows)

If symlinks fail:

```bash
pwsh scripts/sync-skills.ps1
```

Re-run on every skill update. Or add to `bootstrap.sh`.

## Contributing

1. Read [AGENTS.md](./AGENTS.md) for architecture rules
2. Check [docs/adr/](./docs/adr/) for existing decisions
3. Write spec for new features in `docs/specs/`
4. Follow the PR template checklist
5. Include tests for failure paths (out of stock, payment timeout, late events)

## Resources

- **Architecture Guide**: [AGENTS.md](./AGENTS.md)
- **Requirements**: [docs/requirements/](./docs/requirements/)
- **ADRs**: [docs/adr/](./docs/adr/)
- **Agent Setup**: [docs/agent-setup.md](./docs/agent-setup.md)
- **MassTransit Docs**: https://masstransit.io/
- **EF Core Docs**: https://learn.microsoft.com/en-us/ef/core/
- **ASP.NET Core Docs**: https://learn.microsoft.com/en-us/aspnet/core/

## License

This project is licensed under the MIT License - see the [LICENSE](./LICENSE) file for details.

## Support

For questions or issues:
- Check [docs/agent-friction.md](./docs/agent-friction.md) for common pitfalls
- Review architecture in [docs/requirements/](./docs/requirements/)
- Open an issue with the `question` label
