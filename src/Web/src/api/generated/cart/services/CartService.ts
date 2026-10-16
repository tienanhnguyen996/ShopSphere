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
   * Retrieve the current customer's cart
   * @returns CartDto OK
   * @throws ApiError
   */
  public static getCart({
    xCustomerId,
  }: {
    /**
     * Customer identifier owning the cart.
     */
    xCustomerId: string;
  }): CancelablePromise<CartDto> {
    return __request(OpenAPI, {
      method: 'GET',
      url: '/api/v1/cart',
      headers: {
        'X-Customer-Id': xCustomerId,
      },
      errors: {
        400: `Bad Request - Missing or invalid customer ID`,
      },
    });
  }
  /**
   * Clear all items from the customer's cart
   * @returns void
   * @throws ApiError
   */
  public static clearCart({
    xCustomerId,
  }: {
    /**
     * Customer identifier owning the cart.
     */
    xCustomerId: string;
  }): CancelablePromise<void> {
    return __request(OpenAPI, {
      method: 'DELETE',
      url: '/api/v1/cart',
      headers: {
        'X-Customer-Id': xCustomerId,
      },
      errors: {
        400: `Bad Request - Missing or invalid customer ID`,
      },
    });
  }
  /**
   * Add an item or increment quantity in the cart
   * @returns CartDto OK
   * @throws ApiError
   */
  public static addItemToCart({
    xCustomerId,
    requestBody,
  }: {
    /**
     * Customer identifier owning the cart.
     */
    xCustomerId: string;
    requestBody: AddCartItemRequest;
  }): CancelablePromise<CartDto> {
    return __request(OpenAPI, {
      method: 'POST',
      url: '/api/v1/cart/items',
      headers: {
        'X-Customer-Id': xCustomerId,
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
   * Update quantity of a specific SKU in the cart
   * @returns CartDto OK
   * @throws ApiError
   */
  public static updateCartItemQuantity({
    sku,
    xCustomerId,
    requestBody,
  }: {
    sku: string;
    /**
     * Customer identifier owning the cart.
     */
    xCustomerId: string;
    requestBody: UpdateCartItemQuantityRequest;
  }): CancelablePromise<CartDto> {
    return __request(OpenAPI, {
      method: 'PUT',
      url: '/api/v1/cart/items/{sku}',
      path: {
        sku: sku,
      },
      headers: {
        'X-Customer-Id': xCustomerId,
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
   * Remove a specific SKU from the cart
   * @returns void
   * @throws ApiError
   */
  public static removeCartItem({
    sku,
    xCustomerId,
  }: {
    sku: string;
    /**
     * Customer identifier owning the cart.
     */
    xCustomerId: string;
  }): CancelablePromise<void> {
    return __request(OpenAPI, {
      method: 'DELETE',
      url: '/api/v1/cart/items/{sku}',
      path: {
        sku: sku,
      },
      headers: {
        'X-Customer-Id': xCustomerId,
      },
      errors: {
        400: `Bad Request`,
        404: `Not Found`,
      },
    });
  }
  /**
   * Generate verified checkout price quote against Catalog Service
   * @returns CheckoutQuoteDto OK
   * @throws ApiError
   */
  public static getCartCheckoutQuote({
    xCustomerId,
  }: {
    /**
     * Customer identifier owning the cart.
     */
    xCustomerId: string;
  }): CancelablePromise<CheckoutQuoteDto> {
    return __request(OpenAPI, {
      method: 'GET',
      url: '/api/v1/cart/quote',
      headers: {
        'X-Customer-Id': xCustomerId,
      },
      errors: {
        400: `Bad Request`,
        503: `Catalog Service Unavailable`,
      },
    });
  }
}
