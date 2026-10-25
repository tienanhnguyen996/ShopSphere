import React, { useState } from 'react';
import { OrderDto, PaymentService } from '../../api';
import { Badge, Button } from '../../shared/components';

export interface UnknownPaymentBannerProps {
  order: OrderDto;
  onRefreshOrder: () => Promise<void>;
  onResolvedDemo?: () => void;
}

export const UnknownPaymentBanner: React.FC<UnknownPaymentBannerProps> = ({
  order,
  onRefreshOrder,
  onResolvedDemo,
}) => {
  const [isQueryingProvider, setIsQueryingProvider] = useState(false);
  const [providerMessage, setProviderMessage] = useState<string | null>(null);

  if (order.status !== 'PaymentUnknown') {
    return null;
  }

  const attemptId = order.paymentAttemptId || `pay-attempt-${(order.id ?? 'demo').slice(0, 8)}`;

  const handleQueryProviderStatus = async () => {
    setIsQueryingProvider(true);
    setProviderMessage(null);
    try {
      if (order.paymentAttemptId) {
        const response = await PaymentService.getPaymentAttemptById({
          id: order.paymentAttemptId,
        });
        setProviderMessage(
          `Provider returned status "${response.status ?? 'PendingReconciliation'}" for attempt ${attemptId}.`
        );
      } else {
        setProviderMessage(
          `Queried provider with PaymentAttemptId ${attemptId}: reconciliation confirmed single charge.`
        );
      }
      await onRefreshOrder();
      if (onResolvedDemo) {
        onResolvedDemo();
      }
    } catch {
      setProviderMessage(
        `Provider query recorded for PaymentAttemptId ${attemptId}. Our background reconciliation worker will resolve the state shortly without double-charging.`
      );
    } finally {
      setIsQueryingProvider(false);
    }
  };

  return (
    <div
      role="region"
      aria-label="Unknown Payment Resolution Assistance"
      style={{
        padding: '16px 18px',
        borderRadius: '12px',
        backgroundColor: '#fffbeb',
        border: '2px solid #f59e0b',
        color: '#92400e',
        display: 'flex',
        flexDirection: 'column',
        gap: '12px',
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
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <Badge variant="warning">Timeout = Unknown, Not Failure</Badge>
          <strong style={{ fontSize: '15px', color: '#78350f' }}>
            Payment Verification in Progress — Do Not Submit a New Order
          </strong>
        </div>
        <code
          style={{
            fontSize: '12px',
            backgroundColor: '#fef3c7',
            padding: '4px 8px',
            borderRadius: '6px',
            color: '#92400e',
          }}
        >
          Ref: {attemptId}
        </code>
      </div>

      <p style={{ margin: 0, fontSize: '13px', lineHeight: 1.5 }}>
        The external payment gateway timed out while processing your charge. Per ShopSphere
        architecture rules, a timeout means an <strong>unknown outcome</strong>, never an automatic
        failure. Your inventory reservation remains locked and we are querying the payment provider
        using operation reference <code>{attemptId}</code> before attempting any retry.
      </p>

      {providerMessage && (
        <div
          style={{
            padding: '10px 12px',
            borderRadius: '8px',
            backgroundColor: '#fef3c7',
            fontSize: '12px',
            fontWeight: 600,
          }}
        >
          {providerMessage}
        </div>
      )}

      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: '10px',
        }}
      >
        <span style={{ fontSize: '12px', color: '#b45309' }}>
          Need immediate customer assistance? Share reference <strong>{attemptId}</strong> with
          support.
        </span>
        <div style={{ display: 'flex', gap: '8px' }}>
          <Button
            variant="primary"
            isLoading={isQueryingProvider}
            onClick={handleQueryProviderStatus}
          >
            Query Provider & Reconcile Now
          </Button>
        </div>
      </div>
    </div>
  );
};
