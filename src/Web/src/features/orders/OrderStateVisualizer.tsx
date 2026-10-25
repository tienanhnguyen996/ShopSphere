import React from 'react';
import { OrderState } from '../../shared/types';
import { Badge, OrderStatusBadge } from '../../shared/components';

export interface OrderStateVisualizerProps {
  status: OrderState;
  onSimulateStateChange?: (state: OrderState) => void;
}

const ALL_STATES: OrderState[] = [
  'Pending',
  'Reserving',
  'Paying',
  'PaymentUnknown',
  'Confirmed',
  'Refunding',
  'Cancelled',
];

function getOrderStateMetadata(status: OrderState): {
  headline: string;
  description: string;
  sagaAction: string;
  accentColor: string;
  bgColor: string;
} {
  switch (status) {
    case 'Pending':
      return {
        headline: 'Order Created — Initializing Checkout Saga',
        description:
          'Your order and idempotency key are persisted in SQL Server with an atomic Outbox message.',
        sagaAction: 'Emitting OrderSubmitted -> InventoryService (ReserveStock)',
        accentColor: '#475569',
        bgColor: '#f8fafc',
      };
    case 'Reserving':
      return {
        headline: 'Locking Inventory Reservation',
        description:
          'InventoryService is acquiring an optimistic concurrency lock (rowversion) and setting a 15-minute reservation TTL.',
        sagaAction: 'Awaiting StockReserved event from InventoryService',
        accentColor: '#0284c7',
        bgColor: '#f0f9ff',
      };
    case 'Paying':
      return {
        headline: 'Authorizing Payment with Provider',
        description:
          'Stock is reserved. PaymentService is executing an idempotent charge using your PaymentAttemptId.',
        sagaAction: 'Awaiting PaymentSucceeded / PaymentFailed / PaymentTimedOut',
        accentColor: '#2563eb',
        bgColor: '#eff6ff',
      };
    case 'PaymentUnknown':
      return {
        headline: 'Payment Provider Timeout — Outcome Unknown (Protected State)',
        description:
          'Timeout != Failure! We paused automatic retries and preserved your stock reservation while querying the payment provider for the exact PaymentAttemptId.',
        sagaAction: 'Reconciliation worker polling PaymentService status before retry/release',
        accentColor: '#d97706',
        bgColor: '#fffbeb',
      };
    case 'Confirmed':
      return {
        headline: 'Order Confirmed & Dispatched to Fulfillment',
        description:
          'Payment succeeded and stock reservation is committed. ShippingService is generating your carrier label and NotificationService sent your confirmation.',
        sagaAction: 'Emitted OrderConfirmed -> ShippingService & NotificationService',
        accentColor: '#16a34a',
        bgColor: '#f0fdf4',
      };
    case 'Refunding':
      return {
        headline: 'Compensating Saga — Refund in Progress',
        description:
          'A late payment event or post-charge cancellation occurred. PaymentService is executing an idempotent refund.',
        sagaAction: 'Awaiting PaymentRefunded -> transitions to Cancelled',
        accentColor: '#ea580c',
        bgColor: '#fff7ed',
      };
    case 'Cancelled':
      return {
        headline: 'Order Cancelled & Stock Released',
        description:
          'All held inventory reservations have been released and any captured funds have been refunded. Late events will not resurrect this order.',
        sagaAction: 'Terminal state — guarded against late events via rowversion check',
        accentColor: '#dc2626',
        bgColor: '#fef2f2',
      };
    default: {
      const _exhaustiveCheck: never = status;
      return _exhaustiveCheck;
    }
  }
}

export const OrderStateVisualizer: React.FC<OrderStateVisualizerProps> = ({
  status,
  onSimulateStateChange,
}) => {
  const metadata = getOrderStateMetadata(status);

  return (
    <div
      style={{
        padding: '18px',
        borderRadius: '12px',
        border: `2px solid ${metadata.accentColor}`,
        backgroundColor: metadata.bgColor,
        display: 'flex',
        flexDirection: 'column',
        gap: '14px',
      }}
    >
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: '10px',
        }}
      >
        <div>
          <div style={{ fontSize: '12px', fontWeight: 700, color: metadata.accentColor }}>
            DISTRIBUTED SAGA STATE MACHINE (ALL 7 STATES)
          </div>
          <h3 style={{ margin: '4px 0 0 0', fontSize: '18px', color: '#0f172a' }}>
            {metadata.headline}
          </h3>
        </div>
        <OrderStatusBadge status={status} />
      </div>

      <p style={{ margin: 0, fontSize: '14px', color: '#334155', lineHeight: 1.5 }}>
        {metadata.description}
      </p>

      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: '8px',
          fontSize: '12px',
          color: '#475569',
          backgroundColor: 'rgba(255, 255, 255, 0.75)',
          padding: '8px 12px',
          borderRadius: '8px',
          border: '1px solid #cbd5e1',
        }}
      >
        <Badge variant="default">Saga Step</Badge>
        <code style={{ fontWeight: 600 }}>{metadata.sagaAction}</code>
      </div>

      <div
        style={{
          display: 'flex',
          flexWrap: 'wrap',
          gap: '8px',
          paddingTop: '6px',
          borderTop: '1px dashed #cbd5e1',
        }}
      >
        {ALL_STATES.map((candidate) => {
          const isCurrent = candidate === status;
          return (
            <button
              key={candidate}
              type="button"
              onClick={() => onSimulateStateChange && onSimulateStateChange(candidate)}
              style={{
                padding: '6px 10px',
                borderRadius: '8px',
                border: isCurrent ? `2px solid ${metadata.accentColor}` : '1px solid #cbd5e1',
                backgroundColor: isCurrent ? '#0f172a' : '#ffffff',
                color: isCurrent ? '#ffffff' : '#334155',
                fontSize: '12px',
                fontWeight: isCurrent ? 700 : 500,
                cursor: onSimulateStateChange ? 'pointer' : 'default',
              }}
            >
              {candidate}
            </button>
          );
        })}
      </div>
    </div>
  );
};
