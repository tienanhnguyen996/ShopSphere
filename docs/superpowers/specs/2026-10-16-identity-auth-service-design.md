# Design Document: ShopSphere In-House Identity & Authorization Service

**Date**: 2026-10-16  
**Status**: Ready for Approval  
**Service Name**: `IdentityService` (`src/Services/Identity/`)  

---

## 1. Intent & Purpose

ShopSphere requires an in-house Authentication and Authorization microservice to manage user accounts, hash credentials securely, issue signed JSON Web Tokens (JWTs), and provide modular, fine-grained permission-based access control (PBAC / RBAC) across all services and the API Gateway. It also seeds a default administrator account on application startup to ensure immediate administrative access.

---

## 2. Goals, Non-Goals & Anti-Goals

### 2.1 Goals
- **In-House Authentication**: Provide secure user registration (`Customer` by default), credential validation, and JWT access + refresh token lifecycle.
- **Modular Fine-Grained Authorization**: Support roles (e.g. `Admin`, `Customer`, `StoreManager`) mapped to fine-grained permissions (e.g. `catalog:products:write`, `catalog:prices:update`, `inventory:stock:adjust`, `orders:read`, `payment:refund`).
- **Fast Local Verification**: Token issuance automatically expands wildcard permissions (like `*:*`) into all concrete permission claims, allowing downstream services and API Gateway to enforce policies using standard ASP.NET Core `RequireClaim("permission", ...)` with zero latency and no custom cross-service evaluator.
- **Admin Seeding**: Automatically seed a default administrator user (`admin@shopsphere.local`) with the `Admin` role and all permissions on initial startup.
- **User-Role Administration**: Provide admin endpoints to assign roles to users, remove roles, and inspect available roles and permissions.
- **Transactional Outbox Integration**: Follow ShopSphere standards (`AGENTS.md`) by publishing domain integration events (`UserRegistered`) via MassTransit and SQL Server Transactional Outbox.
- **Spec & Postman Compliance**: Complete OpenAPI 3.0 specification (`docs/specs/identity-openapi.json`), Postman collection (`docs/postman/Identity.postman_collection.json`), health checks (`/health`), and unit tests.

### 2.2 Non-Goals
- External OAuth2 / OIDC social login (Google, GitHub, Apple) in Phase 1 (modular domain allows future provider plugins).
- Multi-Factor Authentication (SMS/TOTP/FIDO2) in initial release.
- Self-service forgotten password email reset workflows (can be integrated with NotificationService in Phase 2).

### 2.3 Anti-Goals
- Never store raw passwords or weak hashes; use industry-standard salted hashing (`PasswordHasher` with PBKDF2/HMAC-SHA256).
- Never allow cross-database joins between `Identity` and other microservices (`Catalog`, `Cart`, `Payment`, `Inventory`).
- Never perform remote HTTP calls from downstream services back to Identity on every request; authorization claims are packed into verifiable tokens.

---

## 3. Architecture & Domain Model

### 3.1 Domain Aggregates & Entities (`Identity.Domain`)

1. **User**:
   - `Id`: `Guid`
   - `Email`: `string` (unique, normalized lowercase)
   - `PasswordHash`: `string`
   - `FullName`: `string`
   - `IsActive`: `bool`
   - `CreatedAtUtc`: `DateTime`
   - `UpdatedAtUtc`: `DateTime?`
   - Navigation: `ICollection<UserRole> UserRoles`
2. **Role**:
   - `Id`: `Guid`
   - `Name`: `string` (unique, e.g., `Admin`, `Customer`, `StoreManager`)
   - `Description`: `string`
   - Navigation: `ICollection<RolePermission> RolePermissions`
3. **Permission**:
   - `Id`: `Guid`
   - `Code`: `string` (unique, e.g., `catalog:products:create`, `catalog:prices:update`, `inventory:stock:adjust`, `users:manage`)
   - `Description`: `string`
4. **UserRole**:
   - `UserId`: `Guid`, `RoleId`: `Guid`
5. **RolePermission**:
   - `RoleId`: `Guid`, `PermissionId`: `Guid`
6. **RefreshToken**:
   - `Id`: `Guid`
   - `UserId`: `Guid`
   - `TokenHash`: `string`
   - `ExpiresAtUtc`: `DateTime`
   - `CreatedAtUtc`: `DateTime`
   - `RevokedAtUtc`: `DateTime?`
   - `ReplacedByTokenId`: `Guid?`
   - `IsActive`: `bool => RevokedAtUtc == null && DateTime.UtcNow < ExpiresAtUtc`

---

## 4. API Endpoints (`Identity.Api`)

All endpoints are versioned under `/api/v1/auth/` and `/api/v1/users/`:

| Method | Endpoint | Description | Access |
|---|---|---|---|
| `GET` | `/health` | Health status (SQL Server + RabbitMQ checks) | Public |
| `POST` | `/api/v1/auth/register` | Register new customer account | Public |
| `POST` | `/api/v1/auth/login` | Authenticate email + password -> return Access Token + Refresh Token | Public |
| `POST` | `/api/v1/auth/refresh` | Rotate refresh token -> return new Access Token + Refresh Token | Public |
| `POST` | `/api/v1/auth/revoke` | Revoke specific or all active refresh tokens (logout) | Authenticated |
| `GET` | `/api/v1/auth/me` | Current authenticated user profile, roles, and permissions | Authenticated |
| `GET` | `/api/v1/users` | List users with pagination and search | Admin (`users:read`) |
| `GET` | `/api/v1/users/{id}` | Get user detail, roles, and permissions | Admin (`users:read`) |
| `POST` | `/api/v1/users/{id}/roles` | Assign role to user | Admin (`users:manage`) |
| `DELETE` | `/api/v1/users/{id}/roles/{roleId}` | Remove role from user | Admin (`users:manage`) |
| `GET` | `/api/v1/roles` | List available roles and permissions | Admin (`users:read`) |

---

## 5. Security & Token Specification

### 5.1 Access Token (JWT)
- **Algorithm**: HMAC-SHA256 (`HS256`) with configurable secret key (`Jwt:SecretKey`).
- **Lifespan**: 15 minutes.
- **Claims**:
  - `sub`: User ID (`Guid`)
  - `email`: Normalized email
  - `name`: Full name
  - `role`: Array of role names (e.g., `["Admin"]`)
  - `permission`: Array of concrete permission codes (wildcards expanded at token issuance)
  - `iss`: `ShopSphere`
  - `aud`: `ShopSphereApi`

### 5.2 Refresh Token
- Cryptographically secure 64-byte random string (`RandomNumberGenerator`).
- Stored as SHA-256 hash in SQL Server.
- Lifespan: 14 days with sliding expiration and single-use rotation.
- Replay/reuse detection: If an already-rotated token is replayed, its replacement token is also invalidated.

### 5.3 Password Security
- Microsoft `IPasswordHasher<User>` (PBKDF2 with HMAC-SHA512/256, 100,000 iterations, random 128-bit salt).

---

## 6. Seed Data & Startup Routine

On service startup (`IdentityDbSeeder`):
1. **Permissions**:
   - `catalog:products:read`, `catalog:products:create`, `catalog:products:update`, `catalog:prices:update`
   - `inventory:stock:read`, `inventory:stock:adjust`
   - `cart:manage`
   - `payment:read`, `payment:refund`
   - `users:read`, `users:manage`
2. **Roles**:
   - `Admin`: Assigned all permissions.
   - `Customer`: Assigned standard shopping permissions (`cart:manage`, `catalog:products:read`).
3. **Default Admin User**:
   - Email: `admin@shopsphere.local` (configurable via `Identity:DefaultAdmin:Email`)
   - Password: `AdminPassword123!` (configurable via `Identity:DefaultAdmin:Password`)
   - Role: `Admin`

---

## 7. Messaging & Integration Events (`Identity.Application`)

- **Contracts**:
  - `UserRegistered(Guid UserId, string Email, string FullName, DateTime OccurredAtUtc)`
- **Outbox**: MassTransit SQL Server Transactional Outbox tables (`identity.OutboxMessage`, `identity.OutboxState`, `identity.InboxState`).
- Delivered via RabbitMQ to notification or customer-facing services.

---

## 8. Persistence & Infrastructure (`Identity.Infrastructure`)

- **Database**: SQL Server, schema `identity` (`ShopSphere.Identity` database).
- **ORM**: Entity Framework Core 8 with explicit migrations (`IdentityDbContext`).
- **Health Checks**: SQL Server database check and RabbitMQ bus check.

---

## 9. Builder Discretion & Minor Decisions Made
- **Clean Architecture**: Follow established ShopSphere pattern (`Identity.Domain`, `Identity.Application`, `Identity.Infrastructure`, `Identity.Api`).
- **Wildcard Expansion**: Wildcard `*:*` is expanded into all concrete permission claims at token generation to avoid requiring custom authorization handler deployments in downstream services.
- **Token Rotation**: Single-use rotation with `ReplacedByTokenId` tracking for token family replay protection.
- **Role Assignment**: `POST /api/v1/users/{id}/roles` appends a role idempotently; `DELETE /api/v1/users/{id}/roles/{roleId}` removes it.
- **Testing**: Comprehensive Unit tests for Domain and Application handlers, plus API contract validation.
