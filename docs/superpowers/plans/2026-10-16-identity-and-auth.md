# Identity Service & Cross-Service Auth Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the in-house Identity & Authorization Service (`IdentityService`) with a seeded admin account, create shared authentication and authorization extensions in `BuildingBlocks`, and enforce authentication and fine-grained permissions across all services and Gateway.

**Architecture:** Microservice architecture with Clean Architecture inside `IdentityService` (`Domain`, `Application`, `Infrastructure`, `Api`, and `Tests.Unit`). `IdentityService` issues signed JWT access tokens (with pre-expanded permission claims) and sliding refresh tokens. A shared extension library in `BuildingBlocks` enables zero-hop local JWT signature and claim verification in `Catalog`, `Inventory`, `Payment`, `Cart`, and the `Gateway`.

**Tech Stack:** .NET 8, ASP.NET Core Minimal APIs, Entity Framework Core 8 with SQL Server, MassTransit & RabbitMQ (Transactional Outbox), `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.AspNetCore.Identity` (`PasswordHasher<T>`), FluentValidation, xUnit, FluentAssertions.

**Spec:** [`docs/superpowers/specs/2026-10-16-identity-auth-service-design.md`](file:///C:/Users/tiena/project/docs/superpowers/specs/2026-10-16-identity-auth-service-design.md)

## Global Constraints
- Target Framework: `net8.0` with strict nullability (`<Nullable>enable</Nullable>`).
- Conventional Commits with Notion Task ID: `<type>(<scope>): [Task-XX] <description>`.
- Service boundary rule: `IdentityService` owns the `identity` schema in SQL Server; no cross-database access.
- In-house auth: Use PBKDF2/HMAC-SHA512/256 password hashing via `IPasswordHasher<User>`.
- OpenAPI documentation: Every endpoint documented in `docs/specs/identity-openapi.json` and synchronized with Postman via Postman MCP.
- Health checks: `/health` with SQL Server and RabbitMQ dependencies breakdown.

## Review Focus
1. Wildcard permission expansion: Ensure `Admin` user tokens contain all concrete permissions so standard policy checks pass.
2. Expired or revoked refresh token: Attempting to refresh with a revoked token must return `401 Unauthorized` and trigger family invalidation.
3. Invalid login credentials: Wrong password or non-existent email must return standard `401 Unauthorized` without leaking user existence.
4. Duplicate user registration: Registering with an already existing email returns `409 Conflict`.
5. Missing bearer token on protected microservice endpoint (e.g. Catalog price update): Must return `401 Unauthorized`, and token lacking permission must return `403 Forbidden`.

---

## File Structure

### Identity Service (`src/Services/Identity/`)
- `Identity.Domain/`:
  - `User.cs`: User aggregate root (Id, Email, PasswordHash, FullName, IsActive, CreatedAtUtc, UpdatedAtUtc, UserRoles).
  - `Role.cs`: Role entity (Id, Name, Description, RolePermissions).
  - `Permission.cs`: Permission entity (Id, Code, Description).
  - `UserRole.cs`: Many-to-many join entity.
  - `RolePermission.cs`: Many-to-many join entity.
  - `RefreshToken.cs`: Refresh token entity (Id, UserId, TokenHash, ExpiresAtUtc, CreatedAtUtc, RevokedAtUtc, ReplacedByTokenId, IsActive).
  - `Exceptions/IdentityDomainException.cs`.
- `Identity.Application/`:
  - `Common/Interfaces/IIdentityDbContext.cs`, `IJwtTokenGenerator.cs`, `IPasswordHashService.cs`.
  - `Contracts/IdentityContracts.cs`: DTOs (`TokenResponse`, `UserProfileDto`, `RoleDto`, `PermissionDto`) and integration events (`UserRegistered`).
  - `Auth/Commands/Register/`: `RegisterCommand.cs`, `RegisterCommandHandler.cs`, `RegisterCommandValidator.cs`.
  - `Auth/Commands/Login/`: `LoginCommand.cs`, `LoginCommandHandler.cs`, `LoginCommandValidator.cs`.
  - `Auth/Commands/RefreshToken/`: `RefreshTokenCommand.cs`, `RefreshTokenCommandHandler.cs`.
  - `Auth/Commands/RevokeToken/`: `RevokeTokenCommand.cs`, `RevokeTokenCommandHandler.cs`.
  - `Auth/Queries/GetCurrentUser/`: `GetCurrentUserQuery.cs`, `GetCurrentUserQueryHandler.cs`.
  - `Users/Commands/AssignRole/`: `AssignRoleCommand.cs`, `AssignRoleCommandHandler.cs`.
  - `Users/Commands/RemoveRole/`: `RemoveRoleCommand.cs`, `RemoveRoleCommandHandler.cs`.
  - `Users/Queries/GetUsers/`: `GetUsersQuery.cs`, `GetUsersQueryHandler.cs`.
  - `Roles/Queries/GetRoles/`: `GetRolesQuery.cs`, `GetRolesQueryHandler.cs`.
- `Identity.Infrastructure/`:
  - `IdentityDbContext.cs`: EF Core DbContext in schema `identity`.
  - `IdentityDbSeeder.cs`: Seeds core permissions, roles, and default admin account (`admin@shopsphere.local`).
  - `Services/JwtTokenGenerator.cs`: Generates signed JWT with expanded permissions.
  - `Services/PasswordHashService.cs`: Wraps ASP.NET Core `PasswordHasher<User>`.
  - `DependencyInjection.cs`: Service registrations, MassTransit Outbox.
- `Identity.Api/`:
  - `Program.cs`: ASP.NET Core host, Swagger, Observability, HealthChecks.
  - `IdentityApiEndpoints.cs`: Minimal API route mapping.
  - `openapi.json`: OpenAPI 3.0 specification.
- `tests/Identity.Tests.Unit/`:
  - Domain, Application, and TokenGenerator unit tests.

### BuildingBlocks (`src/BuildingBlocks/`)
- `Security/AuthenticationExtensions.cs`: `AddShopSphereAuthentication(IConfiguration)`.
- `Security/AuthorizationExtensions.cs`: `AddShopSphereAuthorization()`, `ShopSpherePermissions.cs`.
- `tests/BuildingBlocks.Tests.Unit/Security/SecurityExtensionsTests.cs`.

### Downstream Services & Gateway Integration
- `src/Services/Catalog/Catalog.Api/Program.cs` & `CatalogApiEndpoints.cs`.
- `src/Services/Inventory/Inventory.Api/Program.cs` & `InventoryApiEndpoints.cs`.
- `src/Services/Payment/Payment.Api/Program.cs` & `PaymentApiEndpoints.cs`.
- `src/Services/Cart/Cart.Api/Program.cs` & `CartApiEndpoints.cs`.
- `src/Gateway/Gateway.Host/Program.cs` & `appsettings.json`.

---

## Tasks

### Task 1: BuildingBlocks — Shared JWT Authentication & Authorization Extensions [Task-65]

**Files:**
- Create: `src/BuildingBlocks/Security/ShopSpherePermissions.cs`
- Create: `src/BuildingBlocks/Security/AuthenticationExtensions.cs`
- Create: `src/BuildingBlocks/Security/AuthorizationExtensions.cs`
- Test: `tests/BuildingBlocks.Tests.Unit/Security/SecurityExtensionsTests.cs`

**Interfaces:**
- Produces:
  - `ShopSpherePermissions`: Static constants (`CatalogRead`, `CatalogCreate`, `CatalogPriceUpdate`, `InventoryAdjust`, `PaymentRead`, `CartManage`, `UsersManage`, `UsersRead`, `SuperAdmin`).
  - `IServiceCollection.AddShopSphereAuthentication(IConfiguration config)`: Configures JWT Bearer authentication.
  - `IServiceCollection.AddShopSphereAuthorization()`: Configures permission-based policies (`RequireClaim("permission", permissionCode)`).

- [ ] **Step 1: Write the failing unit tests for security extensions**
  Test in `tests/BuildingBlocks.Tests.Unit/Security/SecurityExtensionsTests.cs` verifying authentication handler registration and permission policy creation.
- [ ] **Step 2: Run test to verify failure**
  Run: `dotnet test tests/BuildingBlocks.Tests.Unit --filter "FullyQualifiedName~SecurityExtensionsTests"`
  Expected: FAIL (types not found).
- [ ] **Step 3: Implement `ShopSpherePermissions`, `AuthenticationExtensions`, and `AuthorizationExtensions`**
  Implement in `src/BuildingBlocks/Security/`.
- [ ] **Step 4: Run test to verify it passes**
  Run: `dotnet test tests/BuildingBlocks.Tests.Unit --filter "FullyQualifiedName~SecurityExtensionsTests"`
  Expected: PASS.
- [ ] **Step 5: Commit**
  ```bash
  git add src/BuildingBlocks/ tests/BuildingBlocks.Tests.Unit/
  git commit -m "feat(buildingblocks): [Task-65] implement shared JWT authentication and permission authorization extensions"
  ```

---

### Task 2: Identity Service — Domain Aggregates & Value Objects [Task-64]

**Files:**
- Create: `src/Services/Identity/Identity.Domain/User.cs`
- Create: `src/Services/Identity/Identity.Domain/Role.cs`
- Create: `src/Services/Identity/Identity.Domain/Permission.cs`
- Create: `src/Services/Identity/Identity.Domain/UserRole.cs`
- Create: `src/Services/Identity/Identity.Domain/RolePermission.cs`
- Create: `src/Services/Identity/Identity.Domain/RefreshToken.cs`
- Create: `src/Services/Identity/Identity.Domain/Exceptions/IdentityDomainException.cs`
- Test: `tests/Identity.Tests.Unit/Domain/UserDomainTests.cs`
- Test: `tests/Identity.Tests.Unit/Domain/RefreshTokenDomainTests.cs`

**Interfaces:**
- Produces:
  - `User.Create(Guid id, string email, string passwordHash, string fullName)`
  - `User.AssignRole(Guid roleId)`, `User.RemoveRole(Guid roleId)`
  - `Role.Create(Guid id, string name, string description)`
  - `Role.AddPermission(Guid permissionId)`
  - `RefreshToken.Create(Guid id, Guid userId, string tokenHash, DateTime expiresAtUtc)`
  - `RefreshToken.Revoke(DateTime revokedAtUtc, Guid? replacedByTokenId = null)`

- [ ] **Step 1: Write failing domain unit tests**
  Tests in `tests/Identity.Tests.Unit/Domain/` validating user creation, role assignment invariants, and refresh token revocation.
- [ ] **Step 2: Run tests to verify failure**
  Run: `dotnet test tests/Identity.Tests.Unit --filter "FullyQualifiedName~Domain"`
  Expected: FAIL.
- [ ] **Step 3: Implement Domain entities and aggregates**
  Implement `User`, `Role`, `Permission`, `UserRole`, `RolePermission`, `RefreshToken`.
- [ ] **Step 4: Run tests to verify they pass**
  Run: `dotnet test tests/Identity.Tests.Unit --filter "FullyQualifiedName~Domain"`
  Expected: PASS.
- [ ] **Step 5: Commit**
  ```bash
  git add src/Services/Identity/Identity.Domain/ tests/Identity.Tests.Unit/
  git commit -m "feat(identity): [Task-64] implement User, Role, and RefreshToken domain aggregates"
  ```

---

### Task 3: Identity Service — Application CQRS Commands & JWT Token Generation [Task-64]

**Files:**
- Create: `src/Services/Identity/Identity.Application/Common/Interfaces/IIdentityDbContext.cs`
- Create: `src/Services/Identity/Identity.Application/Common/Interfaces/IJwtTokenGenerator.cs`
- Create: `src/Services/Identity/Identity.Application/Common/Interfaces/IPasswordHashService.cs`
- Create: `src/Services/Identity/Identity.Application/Contracts/IdentityContracts.cs`
- Create: `src/Services/Identity/Identity.Application/Auth/Commands/Register/RegisterCommand.cs`
- Create: `src/Services/Identity/Identity.Application/Auth/Commands/Register/RegisterCommandHandler.cs`
- Create: `src/Services/Identity/Identity.Application/Auth/Commands/Login/LoginCommand.cs`
- Create: `src/Services/Identity/Identity.Application/Auth/Commands/Login/LoginCommandHandler.cs`
- Create: `src/Services/Identity/Identity.Application/Auth/Commands/RefreshToken/RefreshTokenCommand.cs`
- Create: `src/Services/Identity/Identity.Application/Auth/Commands/RefreshToken/RefreshTokenCommandHandler.cs`
- Create: `src/Services/Identity/Identity.Application/Auth/Commands/RevokeToken/RevokeTokenCommand.cs`
- Create: `src/Services/Identity/Identity.Application/Auth/Commands/RevokeToken/RevokeTokenCommandHandler.cs`
- Create: `src/Services/Identity/Identity.Application/Auth/Queries/GetCurrentUser/GetCurrentUserQuery.cs`
- Create: `src/Services/Identity/Identity.Application/Auth/Queries/GetCurrentUser/GetCurrentUserQueryHandler.cs`
- Create: `src/Services/Identity/Identity.Application/Users/Commands/AssignRole/AssignRoleCommand.cs`
- Create: `src/Services/Identity/Identity.Application/Users/Commands/AssignRole/AssignRoleCommandHandler.cs`
- Create: `src/Services/Identity/Identity.Application/Users/Commands/RemoveRole/RemoveRoleCommand.cs`
- Create: `src/Services/Identity/Identity.Application/Users/Commands/RemoveRole/RemoveRoleCommandHandler.cs`
- Create: `src/Services/Identity/Identity.Application/Users/Queries/GetUsers/GetUsersQuery.cs`
- Create: `src/Services/Identity/Identity.Application/Roles/Queries/GetRoles/GetRolesQuery.cs`
- Test: `tests/Identity.Tests.Unit/Application/AuthCommandTests.cs`

**Interfaces:**
- Produces:
  - `RegisterCommand(string Email, string Password, string FullName)` -> `Result<TokenResponse>`
  - `LoginCommand(string Email, string Password)` -> `Result<TokenResponse>`
  - `RefreshTokenCommand(string RefreshToken)` -> `Result<TokenResponse>`
  - `RevokeTokenCommand(Guid UserId, string? RefreshToken, bool RevokeAll)` -> `Result`
  - `GetCurrentUserQuery(Guid UserId)` -> `Result<UserProfileDto>`

- [ ] **Step 1: Write failing unit tests for Application commands**
  Unit tests covering Register, Login (valid/invalid), and RefreshToken with mocked DbContext and TokenGenerator.
- [ ] **Step 2: Run tests to verify failure**
  Run: `dotnet test tests/Identity.Tests.Unit --filter "FullyQualifiedName~AuthCommandTests"`
  Expected: FAIL.
- [ ] **Step 3: Implement CQRS commands, handlers, and validation rules**
- [ ] **Step 4: Run tests to verify they pass**
  Run: `dotnet test tests/Identity.Tests.Unit --filter "FullyQualifiedName~AuthCommandTests"`
  Expected: PASS.
- [ ] **Step 5: Commit**
  ```bash
  git add src/Services/Identity/Identity.Application/ tests/Identity.Tests.Unit/
  git commit -m "feat(identity): [Task-64] implement registration, login, token refresh, and user management CQRS handlers"
  ```

---

### Task 4: Identity Service — Infrastructure, EF Core Persistence & Default Admin Seeder [Task-64]

**Files:**
- Create: `src/Services/Identity/Identity.Infrastructure/IdentityDbContext.cs`
- Create: `src/Services/Identity/Identity.Infrastructure/IdentityDbContextFactory.cs`
- Create: `src/Services/Identity/Identity.Infrastructure/IdentityDbSeeder.cs`
- Create: `src/Services/Identity/Identity.Infrastructure/Services/JwtTokenGenerator.cs`
- Create: `src/Services/Identity/Identity.Infrastructure/Services/PasswordHashService.cs`
- Create: `src/Services/Identity/Identity.Infrastructure/DependencyInjection.cs`
- Test: `tests/Identity.Tests.Unit/Infrastructure/JwtTokenGeneratorTests.cs`
- Test: `tests/Identity.Tests.Unit/Infrastructure/IdentityDbSeederTests.cs`

**Interfaces:**
- Produces:
  - `IdentityDbContext`: Schema `identity`, tables `Users`, `Roles`, `Permissions`, `UserRoles`, `RolePermissions`, `RefreshTokens`, MassTransit Outbox.
  - `IdentityDbSeeder.SeedAsync(IdentityDbContext context)`: Seeds core permissions, `Admin` & `Customer` roles, and default admin user `admin@shopsphere.local` with password `AdminPassword123!`.
  - `JwtTokenGenerator.GenerateToken(User user, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> permissions)`: Issues signed JWT token with expanded permissions.

- [ ] **Step 1: Write failing unit tests for JWT generation and Seeder**
  Verify wildcard expansion in JWT claims and verifying seeder creates admin with hashed password.
- [ ] **Step 2: Run tests to verify failure**
  Run: `dotnet test tests/Identity.Tests.Unit --filter "FullyQualifiedName~JwtTokenGeneratorTests"`
  Expected: FAIL.
- [ ] **Step 3: Implement Infrastructure services, DbContext, and DbSeeder**
- [ ] **Step 4: Run tests to verify they pass**
  Run: `dotnet test tests/Identity.Tests.Unit --filter "FullyQualifiedName~JwtTokenGeneratorTests"`
  Expected: PASS.
- [ ] **Step 5: Commit**
  ```bash
  git add src/Services/Identity/Identity.Infrastructure/ tests/Identity.Tests.Unit/
  git commit -m "feat(identity): [Task-64] implement IdentityDbContext, password hashing, JWT generator, and admin account seeder"
  ```

---

### Task 5: Identity Service — Web API Endpoints, OpenAPI Spec & Postman Collection [Task-64]

**Files:**
- Create: `src/Services/Identity/Identity.Api/Program.cs`
- Create: `src/Services/Identity/Identity.Api/IdentityApiEndpoints.cs`
- Create: `src/Services/Identity/Identity.Api/appsettings.json`
- Create: `docs/specs/identity-openapi.json`
- Create: `src/Services/Identity/Identity.Api/openapi.json`
- Create: `docs/postman/Identity.postman_collection.json`
- Test: `tests/Identity.Tests.Unit/Api/IdentityApiEndpointTests.cs`

**Interfaces:**
- Produces:
  - `MapIdentityEndpoints()`: Maps `/api/v1/auth/` and `/api/v1/users/` routes with OpenAPI metadata and `/health`.
  - Synchronized OpenAPI 3.0 specs and Postman collection with MCP tools.

- [ ] **Step 1: Write endpoint routing and contract tests**
- [ ] **Step 2: Run tests to verify failure**
- [ ] **Step 3: Implement `IdentityApiEndpoints.cs`, `Program.cs`, and OpenAPI specs**
- [ ] **Step 4: Run tests to verify they pass**
  Run: `dotnet test tests/Identity.Tests.Unit`
  Expected: PASS.
- [ ] **Step 5: Synchronize OpenAPI & Collections to Postman MCP**
  Use `createSpec` and `createCollection` for `ShopSphere - Identity Service`.
- [ ] **Step 6: Commit**
  ```bash
  git add src/Services/Identity/Identity.Api/ docs/specs/identity-openapi.json docs/postman/Identity.postman_collection.json
  git commit -m "feat(identity): [Task-64] expose auth and user administration Minimal APIs and sync OpenAPI spec with Postman"
  ```

---

### Task 6: Cross-Service Authentication & Policy-Based Authorization Enforcement [Task-66]

**Files:**
- Modify: `src/Services/Catalog/Catalog.Api/Program.cs` & `CatalogApiEndpoints.cs`
- Modify: `src/Services/Inventory/Inventory.Api/Program.cs` & `InventoryApiEndpoints.cs`
- Modify: `src/Services/Payment/Payment.Api/Program.cs` & `PaymentApiEndpoints.cs`
- Modify: `src/Services/Cart/Cart.Api/Program.cs` & `CartApiEndpoints.cs`
- Modify: `src/Gateway/Gateway.Host/Program.cs` & `appsettings.json`
- Test: `tests/Catalog.Tests.Unit/CatalogAuthTests.cs`
- Test: `tests/Inventory.Tests.Unit/InventoryAuthTests.cs`

**Interfaces:**
- Consumes:
  - `AddShopSphereAuthentication(IConfiguration)` and `AddShopSphereAuthorization()` from `BuildingBlocks`.
- Protects:
  - `Catalog.Api`: `POST /api/v1/catalog/products` (`catalog:products:create`), `PUT /api/v1/catalog/products/{sku}/price` (`catalog:prices:update`).
  - `Inventory.Api`: `POST /api/v1/inventory/adjustments` (`inventory:stock:adjust`).
  - `Payment.Api`: `GET /api/v1/payment/attempts/{id}` (`payment:read`).
  - `Cart.Api`: Authenticates customer token, automatically binding customer ID from `sub` claim.
  - `Gateway`: Validates incoming JWTs and passes downstream claims via headers.

- [ ] **Step 1: Write integration/unit tests for protected endpoints**
  Verify that unauthenticated requests receive 401 Unauthorized, and requests lacking required permission receive 403 Forbidden.
- [ ] **Step 2: Run tests to verify failure**
- [ ] **Step 3: Add `AddShopSphereAuthentication()` and `RequireAuthorization()` to microservice endpoints**
- [ ] **Step 4: Run all test suites across services**
  Run: `dotnet test tests/Catalog.Tests.Unit tests/Inventory.Tests.Unit tests/Payment.Tests.Unit tests/Cart.Tests.Unit`
  Expected: PASS.
- [ ] **Step 5: Commit**
  ```bash
  git add src/Services/ tests/ src/Gateway/
  git commit -m "feat(services): [Task-66] enforce JWT bearer authentication and granular permission authorization policies across microservices"
  ```

---

## Execution Handoff

Please review the plan. Which execution approach would you prefer?

- **Subagent-driven** - A fresh subagent implements each task and a fresh reviewer checks it before the next one starts, then a whole-branch review at the end. Most thorough; costs a fresh context per task and per review.
- **Native** - I implement every task myself in this session, the way this harness runs work, then one fresh reviewer on the most capable model checks the whole branch. Cheapest and fastest; no independent review until the end.

For this plan I recommend **Subagent-driven**, because the tasks span domain modeling, persistence, token generation, API exposure, and cross-service policy enforcement where clean verification per task prevents regressions. Does the plan capture what you want, and which approach should we use?
