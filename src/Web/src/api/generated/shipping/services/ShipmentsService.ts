/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { CancelShipmentRequestDto } from '../models/CancelShipmentRequestDto';
import type { RecordDeliveryRequestDto } from '../models/RecordDeliveryRequestDto';
import type { RecordHandoverRequestDto } from '../models/RecordHandoverRequestDto';
import type { ShipmentDto } from '../models/ShipmentDto';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class ShipmentsService {
  /**
   * Get a shipment by its identifier.
   * @returns ShipmentDto OK
   * @throws ApiError
   */
  public static getShipmentById({
    shipmentId,
  }: {
    shipmentId: string;
  }): CancelablePromise<ShipmentDto> {
    return __request(OpenAPI, {
      method: 'GET',
      url: '/api/v1/shipments/{shipmentId}',
      path: {
        shipmentId: shipmentId,
      },
      errors: {
        404: `Not Found`,
      },
    });
  }
  /**
   * Get a shipment by order identifier.
   * @returns ShipmentDto OK
   * @throws ApiError
   */
  public static getShipmentByOrderId({
    orderId,
  }: {
    orderId: string;
  }): CancelablePromise<ShipmentDto> {
    return __request(OpenAPI, {
      method: 'GET',
      url: '/api/v1/shipments/orders/{orderId}',
      path: {
        orderId: orderId,
      },
      errors: {
        404: `Not Found`,
      },
    });
  }
  /**
   * Get shipment tracking details and milestones by tracking number.
   * @returns ShipmentDto OK
   * @throws ApiError
   */
  public static getShipmentByTrackingNumber({
    trackingNumber,
  }: {
    trackingNumber: string;
  }): CancelablePromise<ShipmentDto> {
    return __request(OpenAPI, {
      method: 'GET',
      url: '/api/v1/shipments/tracking/{trackingNumber}',
      path: {
        trackingNumber: trackingNumber,
      },
      errors: {
        404: `Not Found`,
      },
    });
  }
  /**
   * Record physical carrier handover at the warehouse and emit ShipmentHandedOver.
   * @returns ShipmentDto OK
   * @throws ApiError
   */
  public static recordCarrierHandover({
    shipmentId,
    requestBody,
  }: {
    shipmentId: string;
    requestBody: RecordHandoverRequestDto;
  }): CancelablePromise<ShipmentDto> {
    return __request(OpenAPI, {
      method: 'POST',
      url: '/api/v1/shipments/{shipmentId}/handover',
      path: {
        shipmentId: shipmentId,
      },
      body: requestBody,
      mediaType: 'application/json',
      errors: {
        404: `Not Found`,
        409: `Conflict`,
      },
    });
  }
  /**
   * Record final package delivery to the customer.
   * @returns ShipmentDto OK
   * @throws ApiError
   */
  public static recordShipmentDelivery({
    shipmentId,
    requestBody,
  }: {
    shipmentId: string;
    requestBody: RecordDeliveryRequestDto;
  }): CancelablePromise<ShipmentDto> {
    return __request(OpenAPI, {
      method: 'POST',
      url: '/api/v1/shipments/{shipmentId}/deliver',
      path: {
        shipmentId: shipmentId,
      },
      body: requestBody,
      mediaType: 'application/json',
      errors: {
        404: `Not Found`,
        409: `Conflict`,
      },
    });
  }
  /**
   * Cancel a shipment prior to carrier physical handover.
   * @returns ShipmentDto OK
   * @throws ApiError
   */
  public static cancelShipment({
    shipmentId,
    requestBody,
  }: {
    shipmentId: string;
    requestBody: CancelShipmentRequestDto;
  }): CancelablePromise<ShipmentDto> {
    return __request(OpenAPI, {
      method: 'POST',
      url: '/api/v1/shipments/{shipmentId}/cancel',
      path: {
        shipmentId: shipmentId,
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
