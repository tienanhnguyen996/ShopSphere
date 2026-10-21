/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { CheckoutQuoteItemDto } from './CheckoutQuoteItemDto';
export type CheckoutQuoteDto = {
  customerId: string;
  items: Array<CheckoutQuoteItemDto>;
  subtotal: number;
  currency: string;
  priceVersions: Record<string, number>;
  isValid: boolean;
  validationErrors: Array<string>;
};
