/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AddCartItemRequest } from '../models/AddCartItemRequest';
import type { CartDto } from '../models/CartDto';
import type { CheckoutQuoteDto } from '../models/CheckoutQuoteDto';
import type { UpdateCartItemQuantityRequest } from '../models/UpdateCartItemQuantityRequest';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class CartService {
  /**
   * @returns CartDto OK
   * @throws ApiError
   */
  public static getCart(): CancelablePromise<CartDto> {
    return __request(OpenAPI, {
      method: 'GET',
      url: '/api/v1/cart',
      errors: {
        400: `Bad Request`,
        404: `Not Found`,
      },
    });
  }
  /**
   * @returns void
   * @throws ApiError
   */
  public static clearCart(): CancelablePromise<void> {
    return __request(OpenAPI, {
      method: 'DELETE',
      url: '/api/v1/cart',
      errors: {
        400: `Bad Request`,
        404: `Not Found`,
      },
    });
  }
  /**
   * @returns CartDto OK
   * @throws ApiError
   */
  public static addItemToCart({
    requestBody,
  }: {
    requestBody: AddCartItemRequest;
  }): CancelablePromise<CartDto> {
    return __request(OpenAPI, {
      method: 'POST',
      url: '/api/v1/cart/items',
      body: requestBody,
      mediaType: 'application/json',
      errors: {
        400: `Bad Request`,
        404: `Not Found`,
      },
    });
  }
  /**
   * @returns CartDto OK
   * @throws ApiError
   */
  public static updateCartItemQuantity({
    sku,
    requestBody,
  }: {
    sku: string;
    requestBody: UpdateCartItemQuantityRequest;
  }): CancelablePromise<CartDto> {
    return __request(OpenAPI, {
      method: 'PUT',
      url: '/api/v1/cart/items/{sku}',
      path: {
        sku: sku,
      },
      body: requestBody,
      mediaType: 'application/json',
      errors: {
        400: `Bad Request`,
        404: `Not Found`,
      },
    });
  }
  /**
   * @returns void
   * @throws ApiError
   */
  public static removeCartItem({ sku }: { sku: string }): CancelablePromise<void> {
    return __request(OpenAPI, {
      method: 'DELETE',
      url: '/api/v1/cart/items/{sku}',
      path: {
        sku: sku,
      },
      errors: {
        400: `Bad Request`,
        404: `Not Found`,
      },
    });
  }
  /**
   * @returns CheckoutQuoteDto OK
   * @throws ApiError
   */
  public static getCartCheckoutQuote(): CancelablePromise<CheckoutQuoteDto> {
    return __request(OpenAPI, {
      method: 'GET',
      url: '/api/v1/cart/quote',
      errors: {
        400: `Bad Request`,
        503: `Service Unavailable`,
      },
    });
  }
}
