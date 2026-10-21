/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { CreateProductRequest } from '../models/CreateProductRequest';
import type { PagedProductResult } from '../models/PagedProductResult';
import type { ProductDto } from '../models/ProductDto';
import type { UpdatePriceRequest } from '../models/UpdatePriceRequest';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class ProductsService {
  /**
   * Create a new catalog product
   * @returns ProductDto Created
   * @throws ApiError
   */
  public static createCatalogProduct({
    requestBody,
  }: {
    requestBody: CreateProductRequest;
  }): CancelablePromise<ProductDto> {
    return __request(OpenAPI, {
      method: 'POST',
      url: '/api/v1/catalog/products',
      body: requestBody,
      mediaType: 'application/json',
      errors: {
        400: `Bad Request`,
        409: `Conflict - Product SKU already exists`,
      },
    });
  }
  /**
   * Retrieve paginated products with filtering
   * @returns PagedProductResult OK
   * @throws ApiError
   */
  public static getCatalogProducts({
    page = 1,
    pageSize = 20,
    onlyActive = true,
  }: {
    page?: number;
    pageSize?: number;
    onlyActive?: boolean;
  }): CancelablePromise<PagedProductResult> {
    return __request(OpenAPI, {
      method: 'GET',
      url: '/api/v1/catalog/products',
      query: {
        page: page,
        pageSize: pageSize,
        onlyActive: onlyActive,
      },
    });
  }
  /**
   * Retrieve product details by unique SKU
   * @returns ProductDto OK
   * @throws ApiError
   */
  public static getCatalogProductBySku({ sku }: { sku: string }): CancelablePromise<ProductDto> {
    return __request(OpenAPI, {
      method: 'GET',
      url: '/api/v1/catalog/products/{sku}',
      path: {
        sku: sku,
      },
      errors: {
        404: `Not Found`,
      },
    });
  }
  /**
   * Update product price and increment price version
   * @returns ProductDto OK
   * @throws ApiError
   */
  public static updateCatalogProductPrice({
    sku,
    requestBody,
  }: {
    sku: string;
    requestBody: UpdatePriceRequest;
  }): CancelablePromise<ProductDto> {
    return __request(OpenAPI, {
      method: 'PUT',
      url: '/api/v1/catalog/products/{sku}/price',
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
}
