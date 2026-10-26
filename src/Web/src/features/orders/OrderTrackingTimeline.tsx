import React, { useState } from 'react';
import { ShipmentDto, ShipmentsService } from '../../api';
import { Badge, Button } from '../../shared/components';
import { normalizeShipmentState, ShipmentState } from '../../shared/types';

export interface OrderTrackingTimelineProps {
  shipment: ShipmentDto | null;
  onShipmentUpdated: (shipment: ShipmentDto) => void;
}

function getShipmentBadgeVariant(
  state: ShipmentState
): 'default' | 'info' | 'warning' | 'success' | 'danger' {
  switch (state) {
    case 'Pending':
      return 'default';
    case 'LabelCreated':
    case 'InTransit':
      return 'info';
    case 'ShipmentUnknown':
      return 'warning';
    case 'HandedOver':
    case 'Delivered':
      return 'success';
    case 'Cancelled':
    case 'Failed':
      return 'danger';
    default: {
      const _exhaustive: never = state;
      return _exhaustive;
    }
  }
}

export const OrderTrackingTimeline: React.FC<OrderTrackingTimelineProps> = ({
  shipment,
  onShipmentUpdated,
}) => {
  const [isMutating, setIsMutating] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  if (!shipment) {
    return (
      <div
        style={{
          padding: '16px',
          borderRadius: '12px',
          border: '1px solid #e2e8f0',
          backgroundColor: '#ffffff',
          color: '#64748b',
          fontSize: '13px',
        }}
      >
        Shipment label and carrier tracking timeline will appear once the order reaches{' '}
        <strong>Confirmed</strong>.
      </div>
    );
  }

  const shipmentState = normalizeShipmentState(shipment.status ?? undefined);
  const milestones = shipment.milestones ?? [];
  const isHandedOverOrDelivered =
    shipmentState === 'HandedOver' ||
    shipmentState === 'InTransit' ||
    shipmentState === 'Delivered';

  const resolveTargetShipmentId = async (): Promise<string> => {
    const currentId = shipment.id ?? '';
    if (currentId && !currentId.startsWith('demo-')) {
      return currentId;
    }
    if (shipment.orderId && !shipment.orderId.startsWith('demo-')) {
      try {
        const fetched = await ShipmentsService.getShipmentByOrderId({ orderId: shipment.orderId });
        if (fetched.id) {
          onShipmentUpdated(fetched);
          return fetched.id;
        }
      } catch {
        // Fall through to deterministic UUID for API call
      }
    }
    return '00000000-0000-0000-0000-000000009042';
  };

  const handleRecordHandover = async () => {
    setIsMutating(true);
    setActionError(null);
    const isDemoShipment = !shipment.id || shipment.id.startsWith('demo-');
    try {
      const targetShipmentId = await resolveTargetShipmentId();
      const updated = await ShipmentsService.recordCarrierHandover({
        shipmentId: targetShipmentId,
        requestBody: {
          warehouseLocation: 'Austin Regional Hub, TX',
          notes: `SCAN-${Date.now().toString(36).toUpperCase()}`,
        },
      });
      onShipmentUpdated(updated);
    } catch (err: unknown) {
      if (isDemoShipment) {
        const now = new Date().toISOString();
        onShipmentUpdated({
          ...shipment,
          status: 'HandedOver',
          handedOverAtUtc: now,
          milestones: [
            ...milestones,
            {
              id: `ms-${Date.now()}`,
              status: 'HandedOver',
              location: 'Austin Regional Hub, TX',
              description:
                'Physical package scanned by carrier; InventoryService notified via ShipmentHandedOver.',
              occurredAtUtc: now,
            },
          ],
        });
      } else {
        setActionError(err instanceof Error ? err.message : 'Unable to record carrier handover.');
      }
    } finally {
      setIsMutating(false);
    }
  };

  const handleRecordDelivery = async () => {
    setIsMutating(true);
    setActionError(null);
    const isDemoShipment = !shipment.id || shipment.id.startsWith('demo-');
    try {
      const targetShipmentId = await resolveTargetShipmentId();
      const updated = await ShipmentsService.recordShipmentDelivery({
        shipmentId: targetShipmentId,
        requestBody: {
          location: 'Front Porch — Signed by Recipient',
          notes: shipment.address?.recipientName ?? 'Customer',
        },
      });
      onShipmentUpdated(updated);
    } catch (err: unknown) {
      if (isDemoShipment) {
        const now = new Date().toISOString();
        onShipmentUpdated({
          ...shipment,
          status: 'Delivered',
          deliveredAtUtc: now,
          milestones: [
            ...milestones,
            {
              id: `ms-${Date.now()}`,
              status: 'Delivered',
              location: 'Customer Destination',
              description: 'Package delivered and signed for at destination.',
              occurredAtUtc: now,
            },
          ],
        });
      } else {
        setActionError(err instanceof Error ? err.message : 'Unable to record package delivery.');
      }
    } finally {
      setIsMutating(false);
    }
  };

  const handleCancelShipment = async () => {
    setIsMutating(true);
    setActionError(null);
    const isDemoShipment = !shipment.id || shipment.id.startsWith('demo-');
    try {
      if (isHandedOverOrDelivered) {
        throw new Error(
          '409 Conflict: Cannot cancel shipment after physical carrier handover. Initiate a customer return instead.'
        );
      }
      const targetShipmentId = await resolveTargetShipmentId();
      const updated = await ShipmentsService.cancelShipment({
        shipmentId: targetShipmentId,
        requestBody: {
          reason: 'Customer requested pre-handover shipment cancellation',
        },
      });
      onShipmentUpdated(updated);
    } catch (err: unknown) {
      if (isDemoShipment && !isHandedOverOrDelivered) {
        const now = new Date().toISOString();
        onShipmentUpdated({
          ...shipment,
          status: 'Cancelled',
          milestones: [
            ...milestones,
            {
              id: `ms-${Date.now()}`,
              status: 'Cancelled',
              location: 'Fulfillment Warehouse',
              description: 'Shipment cancelled prior to carrier handover.',
              occurredAtUtc: now,
            },
          ],
        });
      } else {
        setActionError(err instanceof Error ? err.message : 'Shipment cancellation rejected.');
      }
    } finally {
      setIsMutating(false);
    }
  };

  return (
    <div
      style={{
        padding: '18px',
        borderRadius: '12px',
        border: '1px solid #e2e8f0',
        backgroundColor: '#ffffff',
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
          gap: '8px',
        }}
      >
        <div>
          <h3 style={{ margin: 0, fontSize: '16px', fontWeight: 700, color: '#0f172a' }}>
            Carrier Delivery & Tracking Timeline
          </h3>
          <div style={{ fontSize: '12px', color: '#64748b', marginTop: '2px' }}>
            Carrier: <strong>{shipment.carrier ?? 'ShopSphere Express'}</strong> • Tracking #:{' '}
            <code>{shipment.trackingNumber ?? 'PENDING-LABEL'}</code>
          </div>
        </div>
        <Badge variant={getShipmentBadgeVariant(shipmentState)}>{shipmentState}</Badge>
      </div>

      {actionError && (
        <div
          role="alert"
          style={{
            padding: '10px 12px',
            borderRadius: '8px',
            backgroundColor: '#fef2f2',
            border: '1px solid #fecaca',
            color: '#991b1b',
            fontSize: '12px',
            fontWeight: 600,
          }}
        >
          {actionError}
        </div>
      )}

      <div
        style={{
          display: 'flex',
          flexDirection: 'column',
          gap: '10px',
          borderLeft: '2px solid #cbd5e1',
          marginLeft: '8px',
          paddingLeft: '14px',
        }}
      >
        {milestones.length === 0 ? (
          <div style={{ fontSize: '13px', color: '#64748b' }}>
            Shipping label generated. Waiting for warehouse carrier scan.
          </div>
        ) : (
          milestones.map((ms, idx) => (
            <div key={ms.id ?? idx} style={{ position: 'relative' }}>
              <div
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  fontSize: '13px',
                  fontWeight: 700,
                  color: '#0f172a',
                }}
              >
                <span>
                  {ms.status} — {ms.location ?? 'Fulfillment Hub'}
                </span>
                <span style={{ fontSize: '11px', fontWeight: 500, color: '#64748b' }}>
                  {ms.occurredAtUtc ? new Date(ms.occurredAtUtc).toLocaleTimeString() : 'Just now'}
                </span>
              </div>
              <div style={{ fontSize: '12px', color: '#475569', marginTop: '2px' }}>
                {ms.description}
              </div>
            </div>
          ))
        )}
      </div>

      <div
        style={{
          display: 'flex',
          flexWrap: 'wrap',
          gap: '8px',
          paddingTop: '10px',
          borderTop: '1px solid #f1f5f9',
        }}
      >
        <Button
          variant="secondary"
          disabled={isMutating || shipmentState !== 'LabelCreated'}
          onClick={handleRecordHandover}
        >
          Simulate Carrier Handover
        </Button>
        <Button
          variant="primary"
          disabled={isMutating || shipmentState !== 'HandedOver'}
          onClick={handleRecordDelivery}
        >
          Simulate Final Delivery
        </Button>
        <Button
          variant="danger"
          disabled={isMutating || shipmentState === 'Cancelled' || shipmentState === 'Delivered'}
          onClick={handleCancelShipment}
        >
          Cancel Shipment (Pre-Handover Guard)
        </Button>
      </div>
    </div>
  );
};
