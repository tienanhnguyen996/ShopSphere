/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
export type PaymentAttemptResponse = {
  id?: string;
  orderId?: string;
  amount?: number;
  currency?: string | null;
  paymentMethod?: string | null;
  status?: string | null;
  providerTransactionId?: string | null;
  failureReason?: string | null;
  createdAtUtc?: string;
  updatedAtUtc?: string | null;
};
