import React, { useCallback, useEffect, useRef, useState } from 'react';
import {
  NotificationJobDto,
  NotificationsService,
  OrderDto,
  OrdersService,
  ShipmentDto,
  ShipmentsService,
} from '../../api';
import { Badge, Button } from '../../shared/components';
import { formatCurrencyAmount, normalizeOrderState, OrderState } from '../../shared/types';
import { EmailPreviewCard } from './EmailPreviewCard';
import { OrderStateVisualizer } from './OrderStateVisualizer';
import { OrderTrackingTimeline } from './OrderTrackingTimeline';
import { UnknownPaymentBanner } from './UnknownPaymentBanner';

export interface LiveOrderTrackerProps {
  order: OrderDto;
  onOrderUpdated: (updated: OrderDto) => void;
  onNotifyToast?: (
    title: string,
    description: string,
    variant: 'success' | 'warning' | 'danger' | 'info'
  ) => void;
}

function isTerminalOrderState(state: OrderState): boolean {
  switch (state) {
    case 'Confirmed':
    case 'Cancelled':
      return true;
    case 'Pending':
    case 'Reserving':
    case 'Paying':
    case 'PaymentUnknown':
    case 'Refunding':
      return false;
    default: {
      const _exhaustive: never = state;
      return _exhaustive;
    }
  }
}

export const LiveOrderTracker: React.FC<LiveOrderTrackerProps> = ({
  order,
  onOrderUpdated,
  onNotifyToast,
}) => {
  const [shipment, setShipment] = useState<ShipmentDto | null>(null);
  const [notifications, setNotifications] = useState<NotificationJobDto[]>([]);
  const [pollIntervalMs, setPollIntervalMs] = useState<number>(1000);
  const [isPolling, setIsPolling] = useState<boolean>(false);
  const [isCancelling, setIsCancelling] = useState<boolean>(false);
  const backoffRef = useRef<number>(1000);

  const orderState = normalizeOrderState(order.status ?? undefined);
  const isDemoOrder = !order.id || order.id.startsWith('demo-');

  const refreshOrderArtifacts = useCallback(async () => {
    if (!order.id) return;
    if (isDemoOrder) {
      return;
    }

    setIsPolling(true);
    try {
      const latestOrder = await OrdersService.getOrderById({ id: order.id });
      onOrderUpdated(latestOrder);

      try {
        const latestShipment = await ShipmentsService.getShipmentByOrderId({ orderId: order.id });
        setShipment(latestShipment);
      } catch {
        // Shipment may not exist yet until OrderConfirmed
      }

      try {
        const jobs = await NotificationsService.getNotificationsByOrderId({ orderId: order.id });
        setNotifications(jobs);
      } catch {
        // Notifications may not exist yet
      }
    } catch {
      // Keep current snapshot if gateway is unreachable
    } finally {
      setIsPolling(false);
    }
  }, [isDemoOrder, onOrderUpdated, order.id]);

  // Seed or sync demo shipment and notification previews when order transitions
  useEffect(() => {
    if (orderState === 'Confirmed' && !shipment) {
      const now = new Date().toISOString();
      setShipment({
        id: isDemoOrder
          ? `demo-ship-${order.id ?? '1'}`
          : (order.shipmentId ?? `demo-ship-${order.id ?? '1'}`),
        orderId: order.id,
        customerId: order.customerId,
        status: 'LabelCreated',
        carrier: 'ShopSphere Express',
        trackingNumber:
          order.trackingNumber ?? `SS-TRK-${(order.id ?? '1001').slice(-6).toUpperCase()}`,
        address: {
          recipientName: order.shippingAddress?.recipientName ?? 'Alex Rivera',
          street: order.shippingAddress?.street ?? '742 Evergreen Terrace',
          city: order.shippingAddress?.city ?? 'Austin',
          state: order.shippingAddress?.state ?? 'TX',
          postalCode: order.shippingAddress?.postalCode ?? '78701',
          country: order.shippingAddress?.country ?? 'US',
        },
        createdAtUtc: now,
        milestones: [
          {
            id: 'ms-label',
            status: 'LabelCreated',
            location: 'Austin Fulfillment Center',
            description: 'Shipping label created upon OrderConfirmed saga event.',
            occurredAtUtc: now,
          },
        ],
      });
    }

    if ((orderState === 'Confirmed' || orderState === 'Cancelled') && notifications.length === 0) {
      const now = new Date().toISOString();
      setNotifications([
        {
          id: `demo-notif-${order.id ?? '1'}`,
          orderId: order.id,
          customerId: order.customerId,
          notificationType: orderState === 'Confirmed' ? 'OrderConfirmed' : 'OrderCancelled',
          channel: 'Email',
          recipient: 'alex.rivera@example.com',
          subject:
            orderState === 'Confirmed'
              ? `Order ${order.id ?? ''} Confirmed — Ready for Carrier Handover`
              : `Order ${order.id ?? ''} Cancelled & Refund Initiated`,
          body:
            orderState === 'Confirmed'
              ? `Hello ${order.shippingAddress?.recipientName ?? 'Customer'},\nYour order (${order.currency ?? 'USD'} ${(order.totalAmount ?? 0).toFixed(2)}) has been confirmed. Tracking number: ${order.trackingNumber ?? 'SS-TRK-ACTIVE'}.`
              : `Hello ${order.shippingAddress?.recipientName ?? 'Customer'},\nYour order (${order.id ?? ''}) was cancelled and any reserved stock or payment has been compensated.`,
          status: 'Sent',
          retryCount: 0,
          createdAtUtc: now,
          sentAtUtc: now,
        },
      ]);
    }
  }, [
    isDemoOrder,
    notifications.length,
    order.currency,
    order.customerId,
    order.id,
    order.shipmentId,
    order.shippingAddress,
    order.totalAmount,
    order.trackingNumber,
    orderState,
    shipment,
  ]);

  // Exponential backoff polling while in non-terminal state (1s -> 2s -> 4s -> 8s -> max 15s)
  useEffect(() => {
    if (isTerminalOrderState(orderState)) {
      backoffRef.current = 1000;
      setPollIntervalMs(1000);
      return;
    }

    let timerId: ReturnType<typeof setTimeout> | null = null;
    let isCancelled = false;

    const scheduleNextPoll = () => {
      const delay = backoffRef.current;
      setPollIntervalMs(delay);
      timerId = setTimeout(async () => {
        if (isCancelled) return;
        await refreshOrderArtifacts();
        backoffRef.current = Math.min(backoffRef.current * 2, 15000);
        if (!isCancelled && !isTerminalOrderState(orderState)) {
          scheduleNextPoll();
        }
      }, delay);
    };

    scheduleNextPoll();

    return () => {
      isCancelled = true;
      if (timerId) clearTimeout(timerId);
    };
  }, [orderState, refreshOrderArtifacts]);

  const handleSimulateStateChange = (nextState: OrderState) => {
    const updated: OrderDto = {
      ...order,
      status: nextState,
      updatedAtUtc: new Date().toISOString(),
    };
    onOrderUpdated(updated);
    if (onNotifyToast) {
      onNotifyToast(
        `Order State -> ${nextState}`,
        `Order ${order.id ?? ''} transitioned to ${nextState}.`,
        nextState === 'Confirmed'
          ? 'success'
          : nextState === 'PaymentUnknown'
            ? 'warning'
            : nextState === 'Cancelled'
              ? 'danger'
              : 'info'
      );
    }
  };

  const handleCancelOrder = async () => {
    if (!order.id) return;
    setIsCancelling(true);
    try {
      if (!isDemoOrder) {
        const cancelled = await OrdersService.cancelOrder({
          id: order.id,
          requestBody: { reason: 'Customer requested cancellation from LiveOrderTracker' },
        });
        onOrderUpdated(cancelled);
      } else {
        handleSimulateStateChange(orderState === 'Confirmed' ? 'Refunding' : 'Cancelled');
      }
    } catch {
      handleSimulateStateChange('Cancelled');
    } finally {
      setIsCancelling(false);
    }
  };

  return (
    <section
      aria-label="Live Order Lifecycle Tracker"
      style={{ display: 'flex', flexDirection: 'column', gap: '18px' }}
    >
      <div
        style={{
          padding: '16px 18px',
          borderRadius: '12px',
          backgroundColor: '#ffffff',
          border: '1px solid #e2e8f0',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: '12px',
        }}
      >
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <h2 style={{ margin: 0, fontSize: '18px', fontWeight: 800, color: '#0f172a' }}>
              Order #{order.id ?? 'N/A'}
            </h2>
            <Badge variant={isTerminalOrderState(orderState) ? 'default' : 'info'}>
              {isTerminalOrderState(orderState)
                ? 'Polling Paused (Terminal State)'
                : `Backoff Polling: ${pollIntervalMs / 1000}s`}
            </Badge>
          </div>
          <div style={{ fontSize: '13px', color: '#64748b', marginTop: '4px' }}>
            Total:{' '}
            <strong>{formatCurrencyAmount(order.totalAmount ?? 0, order.currency, true)}</strong> •
            Payment: <code>{order.paymentMethod ?? 'credit_card'}</code>
          </div>
        </div>

        <div style={{ display: 'flex', gap: '8px' }}>
          <Button
            variant="secondary"
            isLoading={isPolling}
            onClick={() => {
              backoffRef.current = 1000;
              setPollIntervalMs(1000);
              void refreshOrderArtifacts();
            }}
          >
            Refresh Now
          </Button>
          {orderState !== 'Cancelled' && (
            <Button variant="danger" isLoading={isCancelling} onClick={handleCancelOrder}>
              Cancel Order
            </Button>
          )}
        </div>
      </div>

      <UnknownPaymentBanner
        order={order}
        onRefreshOrder={refreshOrderArtifacts}
        onResolvedDemo={() => handleSimulateStateChange('Confirmed')}
      />

      <OrderStateVisualizer status={orderState} onSimulateStateChange={handleSimulateStateChange} />

      <OrderTrackingTimeline
        shipment={shipment}
        onShipmentUpdated={(updatedShipment) => {
          setShipment(updatedShipment);
          if (onNotifyToast) {
            onNotifyToast(
              `Shipment ${updatedShipment.status ?? 'Updated'}`,
              `Tracking ${updatedShipment.trackingNumber ?? ''} updated to ${updatedShipment.status}.`,
              updatedShipment.status === 'Cancelled' ? 'danger' : 'success'
            );
          }
        }}
      />

      <EmailPreviewCard
        notifications={notifications}
        onNotificationUpdated={(updatedJob) => {
          setNotifications((prev) =>
            prev.map((item) => (item.id === updatedJob.id ? updatedJob : item))
          );
        }}
      />
    </section>
  );
};
