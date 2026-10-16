/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { CartItemDto } from './CartItemDto';
export type CartDto = {
  customerId: string;
  items: Array<CartItemDto>;
  totalQuantity: number;
  createdAtUtc: string;
  lastModifiedUtc: string;
};
