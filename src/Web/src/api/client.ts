import { OpenAPI as catalogOpenAPI } from './generated/catalog/core/OpenAPI.ts';
import { OpenAPI as cartOpenAPI } from './generated/cart/core/OpenAPI.ts';
import { OpenAPI as inventoryOpenAPI } from './generated/inventory/core/OpenAPI.ts';
import { OpenAPI as paymentOpenAPI } from './generated/payment/core/OpenAPI.ts';
import { OpenAPI as orderOpenAPI } from './generated/order/core/OpenAPI.ts';
import { OpenAPI as shippingOpenAPI } from './generated/shipping/core/OpenAPI.ts';
import { OpenAPI as notificationOpenAPI } from './generated/notification/core/OpenAPI.ts';
import type { OpenAPIConfig } from './generated/catalog/core/OpenAPI.ts';

export {
  catalogOpenAPI,
  cartOpenAPI,
  inventoryOpenAPI,
  paymentOpenAPI,
  orderOpenAPI,
  shippingOpenAPI,
  notificationOpenAPI,
};

export const apiConfigs: OpenAPIConfig[] = [
  catalogOpenAPI,
  cartOpenAPI,
  inventoryOpenAPI,
  paymentOpenAPI,
  orderOpenAPI,
  shippingOpenAPI,
  notificationOpenAPI,
];

let activeCorrelationId: string | undefined;

/**
 * Returns the currently active correlation ID, or generates a new UUID v4 if none is set.
 */
export function getCorrelationId(): string {
  return activeCorrelationId ?? crypto.randomUUID();
}

/**
 * Explicitly sets or clears the active correlation ID.
 * When set to undefined, subsequent calls to getCorrelationId() generate fresh UUIDs.
 */
export function setCorrelationId(id?: string): void {
  activeCorrelationId = id;
}

let activeAuthToken: string | undefined;

/**
 * Sets or clears the active authentication Bearer token.
 */
export function setAuthToken(token: string | undefined): void {
  activeAuthToken = token;
}

/**
 * Returns the currently active authentication Bearer token.
 */
export function getAuthToken(): string | undefined {
  return activeAuthToken;
}

/**
 * Configures all service OpenAPI clients with base path, credentials,
 * correlation ID header resolver, and auth token resolver.
 */
export function configureApiClients(): void {
  for (const config of apiConfigs) {
    config.BASE = '';
    config.WITH_CREDENTIALS = true;
    config.CREDENTIALS = 'include';
    config.HEADERS = async () => ({
      'X-Correlation-Id': getCorrelationId(),
    });
    config.TOKEN = async () => getAuthToken() ?? '';
  }
}

// Automatically apply configuration on module load
configureApiClients();
