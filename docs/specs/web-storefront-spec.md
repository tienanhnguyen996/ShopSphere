# ShopSphere Web Storefront Specification

> **Status**: Approved  
> **Service**: `Web` (`src/Web`)  
> **Tasks**: `Task-08`, `Task-09`, `Task-17`, `Task-18`, `Task-22`, `Task-23`, `Task-30`, `Task-37`, `Task-41`, `Task-46`, `Task-47`, `Task-48`, `Task-53`, `Task-56`, `Task-59`

## 1. Architectural Rules (`AGENTS.md`)
1. **Gateway Only**: The frontend calls the YARP API Gateway (`/api/v1/*`), never individual microservices directly.
2. **Generated OpenAPI Clients Only**: All API calls use the generated TypeScript clients under `src/Web/src/api/generated/{catalog,cart,inventory,payment,order,shipping,notification}`. No hand-written `fetch()` calls.
3. **Strict TypeScript**: `strict: true`, zero `any`, and exhaustive `switch` statements with `never` checks on union types (`OrderState`, `ShipmentStatus`, etc.).
4. **No Cross-Feature Imports**: Features (`features/catalog`, `features/cart`, `features/checkout`, `features/orders`) must never import from one another. Shared primitives and domain types reside in `src/Web/src/shared/`.
5. **Render All States**: Explicitly render all 7 `OrderState` values (`Pending`, `Reserving`, `Paying`, `PaymentUnknown`, `Confirmed`, `Refunding`, `Cancelled`) and all shipment/payment resilience states.

## 2. Feature Modules
- **`src/shared/`**:
  - `Button`, `Badge`, `Spinner`, `Input`, `Modal`, `OrderStatusBadge`, `ErrorBoundary`, `OfflineToast`, `ToastContainer`.
- **`src/features/catalog/`**:
  - `ProductCatalogGrid` (`Task-17`), `ProductDetailModal` (`Task-18`), `StockAvailabilityBadge` (`Task-30`).
- **`src/features/cart/`**:
  - `ShoppingCartDrawer` (`Task-22`), `PriceVerificationBanner` (`Task-23`).
- **`src/features/checkout/`**:
  - `PaymentSelectionForm` (`Task-37`), `CheckoutWizard` with `Idempotency-Key` guard (`Task-41`).
- **`src/features/orders/`**:
  - `LiveOrderTracker` with exponential backoff polling (`Task-46`), `OrderStateVisualizer` (`Task-47`), `UnknownPaymentBanner` (`Task-48`), `OrderTrackingTimeline` (`Task-53`), `EmailPreviewCard` (`Task-56`).
