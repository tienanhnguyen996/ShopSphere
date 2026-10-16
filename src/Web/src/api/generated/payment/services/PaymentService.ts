/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { CreatePaymentAttemptRequest } from '../models/CreatePaymentAttemptRequest';
import type { PaymentAttemptResponse } from '../models/PaymentAttemptResponse';
import type { ProviderWebhookPayload } from '../models/ProviderWebhookPayload';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class PaymentService {
  /**
   * Receives provider webhook callbacks with HMAC-SHA256 signature verification
   * @returns any OK
   * @throws ApiError
   */
  public static providerWebhook({
    requestBody,
    xPaymentSignature,
  }: {
    requestBody: ProviderWebhookPayload;
    xPaymentSignature?: string;
  }): CancelablePromise<any> {
    return __request(OpenAPI, {
      method: 'POST',
      url: '/api/v1/payment/webhooks/mock-provider',
      headers: {
        'X-Payment-Signature': xPaymentSignature,
      },
      body: requestBody,
      mediaType: 'application/json',
      errors: {
        400: `Bad Request`,
        401: `Unauthorized`,
      },
    });
  }
  /**
   * Retrieve details of a payment attempt by identifier
   * @returns PaymentAttemptResponse OK
   * @throws ApiError
   */
  public static getPaymentAttemptById({
    id,
  }: {
    id: string;
  }): CancelablePromise<PaymentAttemptResponse> {
    return __request(OpenAPI, {
      method: 'GET',
      url: '/api/v1/payment/attempts/{id}',
      path: {
        id: id,
      },
      errors: {
        404: `Not Found`,
      },
    });
  }
  /**
   * Create a new payment attempt for an order
   * @returns PaymentAttemptResponse Created
   * @throws ApiError
   */
  public static createPaymentAttempt({
    requestBody,
  }: {
    requestBody: CreatePaymentAttemptRequest;
  }): CancelablePromise<PaymentAttemptResponse> {
    return __request(OpenAPI, {
      method: 'POST',
      url: '/api/v1/payment/attempts',
      body: requestBody,
      mediaType: 'application/json',
    });
  }
  /**
   * Retrieve the latest payment attempt for an order
   * @returns PaymentAttemptResponse OK
   * @throws ApiError
   */
  public static getPaymentAttemptByOrderId({
    orderId,
  }: {
    orderId: string;
  }): CancelablePromise<PaymentAttemptResponse> {
    return __request(OpenAPI, {
      method: 'GET',
      url: '/api/v1/payment/orders/{orderId}',
      path: {
        orderId: orderId,
      },
      errors: {
        404: `Not Found`,
      },
    });
  }
}
