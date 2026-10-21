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
      assert.strictEqual(tokenAfterClear, undefined);
    }
  }
});

test('API Client - all OpenAPI.BASE configs are initialized to /api', () => {
  assert.strictEqual(apiConfigs.length, 4, 'Should configure 4 service clients');

  const namedConfigs = [
    { name: 'catalog', config: catalogOpenAPI },
    { name: 'cart', config: cartOpenAPI },
    { name: 'inventory', config: inventoryOpenAPI },
    { name: 'payment', config: paymentOpenAPI },
  ];

  for (const { name, config } of namedConfigs) {
    assert.strictEqual(config.BASE, '/api', `${name} client BASE should be /api`);
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
