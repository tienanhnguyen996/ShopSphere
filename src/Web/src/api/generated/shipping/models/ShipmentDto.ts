/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { ShipmentAddressResponseDto } from './ShipmentAddressResponseDto';
import type { ShipmentItemResponseDto } from './ShipmentItemResponseDto';
import type { TrackingMilestoneResponseDto } from './TrackingMilestoneResponseDto';
export type ShipmentDto = {
  id?: string;
  orderId?: string;
  customerId?: string | null;
  status?: string | null;
  carrier?: string | null;
  trackingNumber?: string | null;
  externalShipmentRequestId?: string | null;
  address?: ShipmentAddressResponseDto;
  failureReason?: string | null;
  createdAtUtc?: string;
  updatedAtUtc?: string | null;
  handedOverAtUtc?: string | null;
  deliveredAtUtc?: string | null;
  items?: Array<ShipmentItemResponseDto> | null;
  milestones?: Array<TrackingMilestoneResponseDto> | null;
};
