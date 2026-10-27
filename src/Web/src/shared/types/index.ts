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

export type PaymentMethodType =
  'credit_card' | 'digital_wallet' | 'bank_transfer' | 'decline' | 'timeout' | 'network_error';

export const USD_TO_VND_RATE = 25400;

export function isVndPaymentMethod(method?: string | null): boolean {
  if (!method) return false;
  const normalized = method.trim().toLowerCase();
  return (
    normalized === 'digital_wallet' ||
    normalized === 'momo' ||
    normalized === 'ewallet' ||
    normalized === 'bank_transfer' ||
    normalized === 'vnpay' ||
    normalized === 'banking'
  );
}

export function convertUsdToVnd(usdAmount: number): number {
  return Math.round(usdAmount * USD_TO_VND_RATE);
}

export function formatCurrencyAmount(
  amount: number | undefined | null,
  currency?: string | null,
  includeUsdEquivalent = true
): string {
  const safeAmount = amount ?? 0;
  const normalizedCurrency = (currency ?? 'USD').trim().toUpperCase();

  if (normalizedCurrency === 'VND') {
    const formattedVnd = `${Math.round(safeAmount).toLocaleString('en-US')} ₫ VND`;
    if (includeUsdEquivalent && safeAmount > 0) {
      const approxUsd = (safeAmount / USD_TO_VND_RATE).toFixed(2);
      return `${formattedVnd} (~$${approxUsd} USD)`;
    }
    return formattedVnd;
  }

  return `$${safeAmount.toFixed(2)} ${normalizedCurrency}`;
}

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

export interface StateTransitionValidation {
  allowed: boolean;
  reason?: string;
}

const ALLOWED_ORDER_TRANSITIONS: Record<OrderState, readonly OrderState[]> = {
  Pending: ['Reserving', 'Paying', 'Cancelled'],
  Reserving: ['Paying', 'Cancelled'],
  Paying: ['PaymentUnknown', 'Confirmed', 'Refunding', 'Cancelled'],
  PaymentUnknown: ['Confirmed', 'Refunding', 'Cancelled'],
  Confirmed: ['Refunding'],
  Refunding: ['Cancelled'],
  Cancelled: ['Refunding'],
};

export function getAllowedNextOrderStates(current: OrderState): readonly OrderState[] {
  return ALLOWED_ORDER_TRANSITIONS[current] ?? [];
}

export function validateOrderStateTransition(
  from: OrderState,
  to: OrderState,
  shipmentState?: ShipmentState | null
): StateTransitionValidation {
  if (from === to) {
    return {
      allowed: false,
      reason: `Order is already in '${from}' state.`,
    };
  }

  if (
    from === 'Confirmed' &&
    (shipmentState === 'HandedOver' ||
      shipmentState === 'InTransit' ||
      shipmentState === 'Delivered')
  ) {
    return {
      allowed: false,
      reason: `Cannot transition order from 'Confirmed' to '${to}' because shipment is already '${shipmentState}'. Initiate a customer return instead.`,
    };
  }

  if (from === 'Confirmed' && to === 'Cancelled') {
    return {
      allowed: false,
      reason:
        "Cannot directly cancel a 'Confirmed' order without refunding. Transition to 'Refunding' first.",
    };
  }

  if ((from === 'Pending' || from === 'Reserving') && to === 'Refunding') {
    return {
      allowed: false,
      reason: `Cannot transition order from '${from}' to 'Refunding' before the payment stage.`,
    };
  }

  if (from === 'Cancelled' && to === 'Confirmed') {
    return {
      allowed: false,
      reason: "Late events cannot resurrect a 'Cancelled' order into 'Confirmed' state.",
    };
  }

  const allowedTargets = ALLOWED_ORDER_TRANSITIONS[from] ?? [];
  if (!allowedTargets.includes(to)) {
    return {
      allowed: false,
      reason: `Invalid state transition from '${from}' to '${to}'. Allowed next states: ${
        allowedTargets.length > 0 ? allowedTargets.join(', ') : 'none (terminal)'
      }.`,
    };
  }

  return { allowed: true };
}
