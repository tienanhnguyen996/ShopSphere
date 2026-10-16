// Client configuration and correlation ID / auth token management
export {
  configureApiClients,
  getCorrelationId,
  setCorrelationId,
  getAuthToken,
  setAuthToken,
  catalogOpenAPI,
  cartOpenAPI,
  inventoryOpenAPI,
  paymentOpenAPI,
  apiConfigs,
} from './client.ts';

// Common core utilities
export { ApiError } from './generated/catalog/core/ApiError';
export { CancelablePromise, CancelError } from './generated/catalog/core/CancelablePromise';
export type { OpenAPIConfig } from './generated/catalog/core/OpenAPI';

// Catalog Service and DTOs
export { ProductsService } from './generated/catalog';
export type {
  CreateProductRequest,
  PagedProductResult,
  ProductDto,
  UpdatePriceRequest,
} from './generated/catalog';

// Cart Service and DTOs
export { CartService } from './generated/cart';
export type {
  AddCartItemRequest,
  CartDto,
  CartItemDto,
  CheckoutQuoteDto,
  CheckoutQuoteItemDto,
  UpdateCartItemQuantityRequest,
} from './generated/cart';

// Inventory Service and DTOs
export { InventoryService } from './generated/inventory';
export type { StockAdjustmentRequest, StockResponse } from './generated/inventory';

// Payment Service and DTOs
export { PaymentService } from './generated/payment';
export type { PaymentAttemptResponse, ProviderWebhookPayload } from './generated/payment';
