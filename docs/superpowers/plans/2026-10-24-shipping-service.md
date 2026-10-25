# Shipping Service Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the Shipping Service (`Shipping.Domain`, `Shipping.Application`, `Shipping.Infrastructure`, `Shipping.Api`, `Shipping.Worker`, `Shipping.Tests.Unit`) covering Tasks 49–52: `Shipment` aggregate root with tracking milestones and physical handover guards, `CreateShipmentConsumer` with per-order idempotency, `ShipmentHandedOver` physical stock-out event (and Inventory consumer), and Shipment HTTP API + OpenAPI spec.

**Architecture:** Clean Architecture backed by SQL Server (`ShopSphereShipping`, schema `shipping`) with EF Core optimistic concurrency (`RowVersion`) and MassTransit Outbox.

**Tech Stack:** .NET 10, EF Core 10, MassTransit 8.3.3, Swashbuckle OpenAPI 3.0, xUnit, FluentAssertions, Moq.

---

### Task 1: Shipment Domain Aggregate, Tracking Milestones & Handover Invariants ([Task-49])

- [ ] Create `src/Services/Shipping/Shipping.Domain/` (`ShipmentStatus`, `ShippingAddress`, `ShipmentItem`, `TrackingMilestone`, `Shipment`, domain events & exceptions) and unit tests in `tests/Shipping.Tests.Unit/Domain/ShipmentTests.cs`.

### Task 2: EF Core Persistence, Mock Carrier Client & CreateShipment Consumer ([Task-50])

- [ ] Create `src/Services/Shipping/Shipping.Application/` and `src/Services/Shipping/Shipping.Infrastructure/` (`ShippingDbContext`, `MockCarrierProviderClient`, `CreateShipmentConsumer` with `OrderId` idempotency, and `InitialShippingMigration`).
- [ ] Unit tests in `tests/Shipping.Tests.Unit/Infrastructure/ShippingDbContextTests.cs` and `tests/Shipping.Tests.Unit/Application/CreateShipmentConsumerTests.cs`.

### Task 3: Carrier Handover Event & Physical Stock-Out Trigger ([Task-51])

- [ ] Implement `RecordCarrierHandoverCommandHandler` publishing `ShipmentHandedOver` in Outbox and `ShipmentHandedOverConsumer` in `Inventory.Application` for idempotent physical stock-out recording.
- [ ] Unit tests in `tests/Shipping.Tests.Unit/Application/CarrierHandoverTests.cs`.

### Task 4: Shipment HTTP API, Worker & OpenAPI Documentation ([Task-52])

- [ ] Create `src/Services/Shipping/Shipping.Worker/` and `src/Services/Shipping/Shipping.Api/` (`ShippingApiEndpoints.cs`, `Program.cs`, `openapi.json`, `docs/specs/shipping-openapi.json`).
- [ ] Unit tests in `tests/Shipping.Tests.Unit/Api/ShippingApiEndpointTests.cs` and `tests/Shipping.Tests.Unit/OpenApiSpecGeneratorTests.cs`.
