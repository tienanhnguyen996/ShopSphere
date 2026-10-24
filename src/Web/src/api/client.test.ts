import test from 'node:test';
import assert from 'node:assert/strict';
import {
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
} from './client.ts';
import type { ApiRequestOptions } from './generated/catalog/core/ApiRequestOptions.ts';

const UUID_V4_REGEX = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

const dummyOptions: ApiRequestOptions = {
  method: 'GET',
  url: '/api/test',
};

test('API Client - correlation ID generation returns valid UUID v4', () => {
  setCorrelationId(undefined);
  const id1 = getCorrelationId();
  const id2 = getCorrelationId();

  assert.match(id1, UUID_V4_REGEX, 'Generated correlation ID should be a valid UUID v4');
  assert.match(id2, UUID_V4_REGEX, 'Generated correlation ID should be a valid UUID v4');
  assert.notStrictEqual(id1, id2, 'Default correlation IDs should be uniquely generated');
});

test('API Client - custom correlation ID setting and clearing', () => {
  setCorrelationId('test-corr-id-1234');
  assert.strictEqual(getCorrelationId(), 'test-corr-id-1234');

  // Verify multiple calls return the same custom correlation ID
  assert.strictEqual(getCorrelationId(), 'test-corr-id-1234');

  // Clear custom correlation ID
  setCorrelationId(undefined);
  const clearedId = getCorrelationId();
  assert.notStrictEqual(clearedId, 'test-corr-id-1234');
  assert.match(clearedId, UUID_V4_REGEX);
});

test('API Client - HEADERS resolver attaches X-Correlation-Id for all services', async () => {
  for (const config of apiConfigs) {
    assert.strictEqual(
      typeof config.HEADERS,
      'function',
      'config.HEADERS should be a resolver function'
    );
    if (typeof config.HEADERS !== 'function') {
      continue;
    }

    // Test with default generated correlation ID
    setCorrelationId(undefined);
    const headers = await config.HEADERS(dummyOptions);
    assert.ok(headers, 'Headers object should be defined');
    assert.match(headers['X-Correlation-Id'], UUID_V4_REGEX);

    // Test with custom correlation ID
    setCorrelationId('explicit-correlation-uuid');
    const customHeaders = await config.HEADERS(dummyOptions);
    assert.strictEqual(customHeaders['X-Correlation-Id'], 'explicit-correlation-uuid');
  }

  setCorrelationId(undefined);
});

test('API Client - auth token setting, retrieval, and TOKEN resolver', async () => {
  setAuthToken(undefined);
  assert.strictEqual(getAuthToken(), undefined);

  setAuthToken('sample-jwt-token-abcdef');
  assert.strictEqual(getAuthToken(), 'sample-jwt-token-abcdef');

  for (const config of apiConfigs) {
    assert.strictEqual(
      typeof config.TOKEN,
      'function',
      'config.TOKEN should be a resolver function'
    );
    if (typeof config.TOKEN !== 'function') {
      continue;
    }
    const token = await config.TOKEN(dummyOptions);
    assert.strictEqual(token, 'sample-jwt-token-abcdef');
  }

  setAuthToken(undefined);
  assert.strictEqual(getAuthToken(), undefined);

  for (const config of apiConfigs) {
    if (typeof config.TOKEN === 'function') {
      const tokenAfterClear = await config.TOKEN(dummyOptions);
      assert.strictEqual(tokenAfterClear, '');
    }
  }
});

test('API Client - all OpenAPI.BASE configs are initialized to empty string (specs include /api prefix)', () => {
  assert.strictEqual(apiConfigs.length, 7, 'Should configure 7 service clients');

  const namedConfigs = [
    { name: 'catalog', config: catalogOpenAPI },
    { name: 'cart', config: cartOpenAPI },
    { name: 'inventory', config: inventoryOpenAPI },
    { name: 'payment', config: paymentOpenAPI },
    { name: 'order', config: orderOpenAPI },
    { name: 'shipping', config: shippingOpenAPI },
    { name: 'notification', config: notificationOpenAPI },
  ];

  for (const { name, config } of namedConfigs) {
    assert.strictEqual(config.BASE, '', `${name} client BASE should be empty string`);
    assert.strictEqual(
      config.WITH_CREDENTIALS,
      true,
      `${name} client WITH_CREDENTIALS should be true`
    );
    assert.strictEqual(
      config.CREDENTIALS,
      'include',
      `${name} client CREDENTIALS should be "include"`
    );
  }
});

import {
  normalizeOrderState,
  normalizeShipmentState,
  type OrderState,
  type ShipmentState,
} from '../shared/types/index.ts';

test('API Client - exports client aliases matching specification in index.ts', async () => {
  const fs = await import('node:fs');
  const indexPath = new URL('./index.ts', import.meta.url);
  const indexContent = fs.readFileSync(indexPath, 'utf-8');

  assert.match(indexContent, /ProductsService as catalogApi/);
  assert.match(indexContent, /CartService as cartApi/);
  assert.match(indexContent, /InventoryService as inventoryApi/);
  assert.match(indexContent, /PaymentService as paymentApi/);
  assert.match(indexContent, /OrdersService as orderApi/);
  assert.match(indexContent, /ShipmentsService as shippingApi/);
  assert.match(indexContent, /NotificationsService as notificationApi/);
});

test('Shared Domain Types - normalizeOrderState supports all 7 Saga states including PaymentUnknown', () => {
  const allOrderStates: OrderState[] = [
    'Pending',
    'Reserving',
    'Paying',
    'PaymentUnknown',
    'Confirmed',
    'Refunding',
    'Cancelled',
  ];

  for (const state of allOrderStates) {
    assert.strictEqual(normalizeOrderState(state), state);
  }

  assert.strictEqual(normalizeOrderState('UnknownBogusState'), 'Pending');
  assert.strictEqual(normalizeOrderState(undefined), 'Pending');
});

test('Shared Domain Types - normalizeShipmentState supports all 8 Shipping states including ShipmentUnknown and HandedOver', () => {
  const allShipmentStates: ShipmentState[] = [
    'Pending',
    'LabelCreated',
    'ShipmentUnknown',
    'HandedOver',
    'InTransit',
    'Delivered',
    'Cancelled',
    'Failed',
  ];

  for (const state of allShipmentStates) {
    assert.strictEqual(normalizeShipmentState(state), state);
  }

  assert.strictEqual(normalizeShipmentState('InvalidState'), 'Pending');
  assert.strictEqual(normalizeShipmentState(undefined), 'Pending');
});

test('Feature Boundaries - features do not import across feature directories', async () => {
  const fs = await import('node:fs');
  const path = await import('node:path');
  const { fileURLToPath } = await import('node:url');

  const featuresDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../features');
  const featureNames = ['catalog', 'cart', 'checkout', 'orders'];

  for (const feature of featureNames) {
    const dir = path.join(featuresDir, feature);
    const files = fs.readdirSync(dir).filter((f) => f.endsWith('.ts') || f.endsWith('.tsx'));
    for (const file of files) {
      const content = fs.readFileSync(path.join(dir, file), 'utf-8');
      for (const otherFeature of featureNames) {
        if (otherFeature === feature) continue;
        assert.doesNotMatch(
          content,
          new RegExp(`features/${otherFeature}|\\.\\./${otherFeature}`),
          `Feature "${feature}/${file}" must not import from feature "${otherFeature}"`
        );
      }
    }
  }
});
