/**
 * Shared domain states and types across features
 */

export type OrderState =
  'Pending' | 'Reserving' | 'PaymentUnknown' | 'Paying' | 'Confirmed' | 'Cancelled' | 'Refunding';

export type ProductStatus = 'Draft' | 'Active' | 'Discontinued';

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

export interface ApiError {
  code: string;
  message: string;
}
