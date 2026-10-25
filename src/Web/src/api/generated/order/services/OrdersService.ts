/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { CancelOrderRequest } from '../models/CancelOrderRequest';
import type { CheckoutRequest } from '../models/CheckoutRequest';
import type { OrderDto } from '../models/OrderDto';
import type { PagedOrderResult } from '../models/PagedOrderResult';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class OrdersService {
  /**
   * Submit an idempotent checkout request using a verified Cart quote snapshot
   * @returns OrderDto Created
   * @throws ApiError
   */
  public static submitCheckout({
    requestBody,
    idempotencyKey,
  }: {
    requestBody: CheckoutRequest;
    idempotencyKey?: string;
  }): CancelablePromise<OrderDto> {
    return __request(OpenAPI, {
      method: 'POST',
      url: '/api/v1/orders/checkout',
      headers: {
        'Idempotency-Key': idempotencyKey,
      },
      body: requestBody,
      mediaType: 'application/json',
      errors: {
        400: `Bad Request`,
        409: `Conflict`,
        503: `Service Unavailable`,
      },
    });
  }
  /**
   * Retrieve paged orders for the current customer
   * @returns PagedOrderResult OK
   * @throws ApiError
   */
  public static getOrders({
    page = 1,
    pageSize = 20,
  }: {
    page?: number;
    pageSize?: number;
  }): CancelablePromise<PagedOrderResult> {
    return __request(OpenAPI, {
      method: 'GET',
      url: '/api/v1/orders',
      query: {
        page: page,
        pageSize: pageSize,
      },
      errors: {
        400: `Bad Request`,
      },
    });
  }
  /**
   * Retrieve an order by its identifier including status, line items, and shipment tracking
   * @returns OrderDto OK
   * @throws ApiError
   */
  public static getOrderById({ id }: { id: string }): CancelablePromise<OrderDto> {
    return __request(OpenAPI, {
      method: 'GET',
      url: '/api/v1/orders/{id}',
      path: {
        id: id,
      },
      errors: {
        404: `Not Found`,
      },
    });
  }
  /**
   * Cancel a pending or in-flight order
   * @returns OrderDto OK
   * @throws ApiError
   */
  public static cancelOrder({
    id,
    requestBody,
  }: {
    id: string;
    requestBody?: CancelOrderRequest;
  }): CancelablePromise<OrderDto> {
    return __request(OpenAPI, {
      method: 'POST',
      url: '/api/v1/orders/{id}/cancel',
      path: {
        id: id,
      },
      body: requestBody,
      mediaType: 'application/json',
      errors: {
        404: `Not Found`,
        409: `Conflict`,
      },
    });
  }
}
