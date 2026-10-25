import React from 'react';
import { PaymentMethodType } from '../../shared/types';
import { Badge, Button } from '../../shared/components';

export interface PaymentSelectionFormProps {
  selectedMethod: PaymentMethodType;
  onSelectMethod: (method: PaymentMethodType) => void;
  declineAlertReason?: string | null;
  onDismissDeclineAlert?: () => void;
  disabled?: boolean;
}

function getPaymentMethodDetails(method: PaymentMethodType): {
  title: string;
  subtitle: string;
  badgeText: string;
} {
  switch (method) {
    case 'credit_card':
      return {
        title: 'Credit or Debit Card (Stripe)',
        subtitle: 'Worldwide card processing via Stripe sandbox gateway.',
        badgeText: 'Stripe',
      };
    case 'digital_wallet':
      return {
        title: 'MoMo E-Wallet (Vietnam)',
        subtitle: 'Direct MoMo Sandbox gateway & QR checkout.',
        badgeText: 'MoMo',
      };
    case 'bank_transfer':
      return {
        title: 'VNPay Banking / QR (Vietnam)',
        subtitle: 'Direct settlement via VNPay Sandbox gateway.',
        badgeText: 'VNPay',
      };
    default: {
      const _exhaustiveCheck: never = method;
      return _exhaustiveCheck;
    }
  }
}

const PAYMENT_METHODS: PaymentMethodType[] = ['credit_card', 'digital_wallet', 'bank_transfer'];

export const PaymentSelectionForm: React.FC<PaymentSelectionFormProps> = ({
  selectedMethod,
  onSelectMethod,
  declineAlertReason,
  onDismissDeclineAlert,
  disabled = false,
}) => {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h3 style={{ margin: 0, fontSize: '16px', fontWeight: 700, color: '#0f172a' }}>
          Payment Method
        </h3>
        <Badge variant="info">Idempotent Charge Guard</Badge>
      </div>

      {declineAlertReason && (
        <div
          role="alert"
          style={{
            padding: '14px 16px',
            borderRadius: '10px',
            backgroundColor: '#fef2f2',
            border: '1px solid #f87171',
            color: '#991b1b',
            display: 'flex',
            flexDirection: 'column',
            gap: '8px',
          }}
        >
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <strong style={{ fontSize: '14px' }}>Payment Authorization Declined</strong>
            <Badge variant="danger">Card Declined</Badge>
          </div>
          <p style={{ margin: 0, fontSize: '13px', lineHeight: 1.45 }}>{declineAlertReason}</p>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span style={{ fontSize: '12px', color: '#b91c1c' }}>
              Select an alternative payment method or retry safely with your existing idempotency
              key.
            </span>
            {onDismissDeclineAlert && (
              <Button variant="secondary" onClick={onDismissDeclineAlert}>
                Dismiss
              </Button>
            )}
          </div>
        </div>
      )}

      <div style={{ display: 'grid', gap: '10px' }}>
        {PAYMENT_METHODS.map((method) => {
          const details = getPaymentMethodDetails(method);
          const isSelected = selectedMethod === method;
          return (
            <label
              key={method}
              style={{
                display: 'flex',
                alignItems: 'flex-start',
                justifyContent: 'space-between',
                gap: '12px',
                padding: '14px',
                borderRadius: '10px',
                border: isSelected ? '2px solid #2563eb' : '1px solid #cbd5e1',
                backgroundColor: isSelected ? '#eff6ff' : '#ffffff',
                cursor: disabled ? 'not-allowed' : 'pointer',
                opacity: disabled ? 0.65 : 1,
              }}
            >
              <div style={{ display: 'flex', alignItems: 'flex-start', gap: '10px' }}>
                <input
                  type="radio"
                  name="paymentMethod"
                  value={method}
                  checked={isSelected}
                  disabled={disabled}
                  onChange={() => onSelectMethod(method)}
                  style={{ marginTop: '3px' }}
                />
                <div>
                  <div style={{ fontWeight: 700, fontSize: '14px', color: '#0f172a' }}>
                    {details.title}
                  </div>
                  <div style={{ fontSize: '12px', color: '#475569', marginTop: '2px' }}>
                    {details.subtitle}
                  </div>
                </div>
              </div>
              <Badge variant={isSelected ? 'info' : 'default'}>{details.badgeText}</Badge>
            </label>
          );
        })}
      </div>
    </div>
  );
};
