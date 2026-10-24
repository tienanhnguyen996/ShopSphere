/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { NotificationJobDto } from '../models/NotificationJobDto';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class NotificationsService {
  /**
   * Get a notification job by its identifier.
   * @returns NotificationJobDto OK
   * @throws ApiError
   */
  public static getNotificationById({ id }: { id: string }): CancelablePromise<NotificationJobDto> {
    return __request(OpenAPI, {
      method: 'GET',
      url: '/api/v1/notifications/{id}',
      path: {
        id: id,
      },
      errors: {
        404: `Not Found`,
      },
    });
  }
  /**
   * Get all notification jobs dispatched for an order.
   * @returns NotificationJobDto OK
   * @throws ApiError
   */
  public static getNotificationsByOrderId({
    orderId,
  }: {
    orderId: string;
  }): CancelablePromise<Array<NotificationJobDto>> {
    return __request(OpenAPI, {
      method: 'GET',
      url: '/api/v1/notifications/orders/{orderId}',
      path: {
        orderId: orderId,
      },
    });
  }
  /**
   * Retry dispatching a failed notification job.
   * @returns NotificationJobDto OK
   * @throws ApiError
   */
  public static retryNotification({ id }: { id: string }): CancelablePromise<NotificationJobDto> {
    return __request(OpenAPI, {
      method: 'POST',
      url: '/api/v1/notifications/{id}/retry',
      path: {
        id: id,
      },
      errors: {
        404: `Not Found`,
      },
    });
  }
}
