/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { OrderItemDto } from './OrderItemDto';
import type { ShippingAddressRequest } from './ShippingAddressRequest';
export type OrderDto = {
  id?: string;
  customerId?: string | null;
  status?: string | null;
  totalAmount?: number;
  currency?: string | null;
  paymentMethod?: string | null;
  shippingAddress?: ShippingAddressRequest;
  items?: Array<OrderItemDto> | null;
  paymentAttemptId?: string | null;
  shipmentId?: string | null;
  trackingNumber?: string | null;
  failureReason?: string | null;
  cancellationReason?: string | null;
  createdAtUtc?: string;
  updatedAtUtc?: string | null;
};
