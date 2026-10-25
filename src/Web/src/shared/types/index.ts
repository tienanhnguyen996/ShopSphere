/**
 * Shared domain states and types across features
 */

export type OrderState =
  'Pending' | 'Reserving' | 'PaymentUnknown' | 'Paying' | 'Confirmed' | 'Cancelled' | 'Refunding';

export type ShipmentState =
  | 'Pending'
  | 'LabelCreated'
  | 'ShipmentUnknown'
  | 'HandedOver'
  | 'InTransit'
  | 'Delivered'
  | 'Cancelled'
  | 'Failed';

export type ProductStatus = 'Draft' | 'Active' | 'Discontinued';

export type PaymentMethodType = 'credit_card' | 'digital_wallet' | 'bank_transfer';

export type StockLevelStatus = 'in_stock' | 'low_stock' | 'out_of_stock' | 'loading';

export interface Product {
  id: string;
  name: string;
  description: string;
  sku: string;
  price: number;
  currency: string;
  status: ProductStatus;
  priceVersion: number;
}

export interface StockSummary {
  sku: string;
  availableQuantity: number;
  reservedQuantity: number;
}

export interface ToastMessage {
  id: string;
  title: string;
  description: string;
  variant: 'success' | 'warning' | 'danger' | 'info';
}

export interface ApiError {
  code: string;
  message: string;
}

export function normalizeOrderState(raw: string | undefined): OrderState {
  switch (raw) {
    case 'Pending':
    case 'Reserving':
    case 'PaymentUnknown':
    case 'Paying':
    case 'Confirmed':
    case 'Cancelled':
    case 'Refunding':
      return raw;
    default:
      return 'Pending';
  }
}

export function normalizeShipmentState(raw: string | undefined): ShipmentState {
  switch (raw) {
    case 'Pending':
    case 'LabelCreated':
    case 'ShipmentUnknown':
    case 'HandedOver':
    case 'InTransit':
    case 'Delivered':
    case 'Cancelled':
    case 'Failed':
      return raw;
    default:
      return 'Pending';
  }
}
