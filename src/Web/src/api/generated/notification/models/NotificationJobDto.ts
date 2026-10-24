/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
export type NotificationJobDto = {
  id?: string;
  orderId?: string;
  customerId?: string | null;
  notificationType?: string | null;
  channel?: string | null;
  recipient?: string | null;
  subject?: string | null;
  body?: string | null;
  status?: string | null;
  sourceMessageId?: string;
  retryCount?: number;
  providerReferenceId?: string | null;
  lastError?: string | null;
  createdAtUtc?: string;
  sentAtUtc?: string | null;
  updatedAtUtc?: string | null;
};
