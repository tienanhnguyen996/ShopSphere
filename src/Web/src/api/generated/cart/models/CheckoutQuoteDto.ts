/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { CheckoutQuoteItemDto } from './CheckoutQuoteItemDto';
export type CheckoutQuoteDto = {
  customerId?: string | null;
  items?: Array<CheckoutQuoteItemDto> | null;
  subtotal?: number;
  currency?: string | null;
  priceVersions?: Record<string, number> | null;
  isValid?: boolean;
  validationErrors?: Array<string> | null;
};
