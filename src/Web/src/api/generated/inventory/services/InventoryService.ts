/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { StockAdjustmentRequest } from '../models/StockAdjustmentRequest';
import type { StockResponse } from '../models/StockResponse';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class InventoryService {
  /**
   * Adjust stock availability for warehouse intake or inventory corrections
   * @returns StockResponse OK
   * @throws ApiError
   */
  public static adjustStock({
    requestBody,
  }: {
    requestBody: StockAdjustmentRequest;
  }): CancelablePromise<StockResponse> {
    return __request(OpenAPI, {
      method: 'POST',
      url: '/api/v1/inventory/adjustments',
      body: requestBody,
      mediaType: 'application/json',
      errors: {
        400: `Bad Request`,
        404: `Not Found`,
      },
    });
  }
  /**
   * Get real-time stock allocation breakdown for a product SKU
   * @returns StockResponse OK
   * @throws ApiError
   */
  public static getStockBySku({ sku }: { sku: string }): CancelablePromise<StockResponse> {
    return __request(OpenAPI, {
      method: 'GET',
      url: '/api/v1/inventory/{sku}',
      path: {
        sku: sku,
      },
      errors: {
        404: `Not Found`,
      },
    });
  }
}
