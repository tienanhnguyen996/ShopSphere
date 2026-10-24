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
  orderOpenAPI,
  shippingOpenAPI,
  notificationOpenAPI,
  apiConfigs,
} from './client';

// Common core utilities
export { ApiError } from './generated/catalog/core/ApiError';
export { CancelablePromise, CancelError } from './generated/catalog/core/CancelablePromise';
export type { OpenAPIConfig } from './generated/catalog/core/OpenAPI';

// Catalog Service and DTOs
export { ProductsService, ProductsService as catalogApi } from './generated/catalog';
export type {
  CreateProductRequest,
  PagedProductResult,
  ProductDto,
  UpdatePriceRequest,
} from './generated/catalog';

// Cart Service and DTOs
export { CartService, CartService as cartApi } from './generated/cart';
export type {
  AddCartItemRequest,
  CartDto,
  CartItemDto,
  CheckoutQuoteDto,
  CheckoutQuoteItemDto,
  UpdateCartItemQuantityRequest,
} from './generated/cart';

// Inventory Service and DTOs
export { InventoryService, InventoryService as inventoryApi } from './generated/inventory';
export type { StockAdjustmentRequest, StockResponse } from './generated/inventory';

// Payment Service and DTOs
export { PaymentService, PaymentService as paymentApi } from './generated/payment';
export type { PaymentAttemptResponse, ProviderWebhookPayload } from './generated/payment';

// Order Service and DTOs
export { OrdersService, OrdersService as orderApi } from './generated/order';
export type {
  CancelOrderRequest,
  CheckoutRequest,
  OrderDto,
  OrderItemDto,
  PagedOrderResult,
  ShippingAddressRequest,
} from './generated/order';

// Shipping Service and DTOs
export { ShipmentsService, ShipmentsService as shippingApi } from './generated/shipping';
export type {
  CancelShipmentRequestDto,
  RecordDeliveryRequestDto,
  RecordHandoverRequestDto,
  ShipmentAddressResponseDto,
  ShipmentDto,
  ShipmentItemResponseDto,
  TrackingMilestoneResponseDto,
} from './generated/shipping';

// Notification Service and DTOs
export {
  NotificationsService,
  NotificationsService as notificationApi,
} from './generated/notification';
export type { NotificationJobDto } from './generated/notification';
