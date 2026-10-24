import React, { useState } from 'react';
import { CheckoutQuoteDto, OrderDto, OrdersService, ShippingAddressRequest } from '../../api';
import { Badge, Button, Input, Modal } from '../../shared/components';
import { PaymentMethodType } from '../../shared/types';
import { PaymentSelectionForm } from './PaymentSelectionForm';

export interface CheckoutWizardProps {
  isOpen: boolean;
  quote: CheckoutQuoteDto | null;
  onClose: () => void;
  onOrderCreated: (order: OrderDto) => void;
}

function generateIdempotencyKey(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return `chk-${crypto.randomUUID()}`;
  }
  return `chk-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
}

export const CheckoutWizard: React.FC<CheckoutWizardProps> = ({
  isOpen,
  quote,
  onClose,
  onOrderCreated,
}) => {
  const [recipientName, setRecipientName] = useState('Alex Rivera');
  const [street, setStreet] = useState('742 Evergreen Terrace, Suite 200');
  const [city, setCity] = useState('Austin');
  const [state, setState] = useState('TX');
  const [postalCode, setPostalCode] = useState('78701');
  const [country, setCountry] = useState('US');
  const [phone, setPhone] = useState('+1-512-555-0192');
  const [paymentMethod, setPaymentMethod] = useState<PaymentMethodType>('credit_card');
  const [idempotencyKey, setIdempotencyKey] = useState<string>(() => generateIdempotencyKey());
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [declineReason, setDeclineReason] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  const validateAddress = (): boolean => {
    const errors: Record<string, string> = {};
    if (!recipientName.trim()) errors.recipientName = 'Recipient name is required';
    if (!street.trim()) errors.street = 'Street address is required';
    if (!city.trim()) errors.city = 'City is required';
    if (!state.trim()) errors.state = 'State/Province is required';
    if (!postalCode.trim()) errors.postalCode = 'Postal code is required';
    if (!country.trim()) errors.country = 'Country code is required';
    setFieldErrors(errors);
    return Object.keys(errors).length === 0;
  };

  const handleRegenerateIdempotencyKey = () => {
    setIdempotencyKey(generateIdempotencyKey());
    setDeclineReason(null);
  };

  const handleSubmitCheckout = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!validateAddress()) {
      return;
    }

    setIsSubmitting(true);
    setDeclineReason(null);

    const shippingAddress: ShippingAddressRequest = {
      recipientName: recipientName.trim(),
      street: street.trim(),
      city: city.trim(),
      state: state.trim(),
      postalCode: postalCode.trim(),
      country: country.trim(),
      phone: phone.trim() || null,
    };

    try {
      const createdOrder = await OrdersService.submitCheckout({
        idempotencyKey,
        requestBody: {
          shippingAddress,
          paymentMethod,
        },
      });
      onOrderCreated(createdOrder);
    } catch (err: unknown) {
      const message =
        err instanceof Error
          ? err.message
          : 'Gateway timeout or payment authorization rejected. Your idempotency key is preserved so retrying will not double-charge.';
      setDeclineReason(message);
    } finally {
      setIsSubmitting(false);
    }
  };

  const quoteItems = quote?.items ?? [];
  const hasValidationWarnings = (quote?.validationErrors?.length ?? 0) > 0;

  return (
    <Modal
      isOpen={isOpen}
      title="Checkout & Idempotent Order Submission"
      onClose={onClose}
      maxWidthClassName="max-w-2xl"
    >
      <form
        onSubmit={handleSubmitCheckout}
        style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}
      >
        <div
          style={{
            padding: '12px 14px',
            borderRadius: '10px',
            backgroundColor: '#f8fafc',
            border: '1px solid #e2e8f0',
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            flexWrap: 'wrap',
            gap: '8px',
          }}
        >
          <div>
            <div style={{ fontSize: '12px', fontWeight: 700, color: '#334155' }}>
              Active Idempotency-Key Guard
            </div>
            <code style={{ fontSize: '12px', color: '#2563eb' }}>{idempotencyKey}</code>
          </div>
          <Button
            type="button"
            variant="secondary"
            disabled={isSubmitting}
            onClick={handleRegenerateIdempotencyKey}
          >
            New Session Key
          </Button>
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
          <h3 style={{ margin: 0, fontSize: '16px', fontWeight: 700, color: '#0f172a' }}>
            1. Shipping Destination
          </h3>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
            <Input
              label="Recipient Name"
              value={recipientName}
              onChange={(e) => setRecipientName(e.target.value)}
              error={fieldErrors.recipientName}
              disabled={isSubmitting}
            />
            <Input
              label="Phone Number"
              value={phone}
              onChange={(e) => setPhone(e.target.value)}
              disabled={isSubmitting}
            />
          </div>
          <Input
            label="Street Address"
            value={street}
            onChange={(e) => setStreet(e.target.value)}
            error={fieldErrors.street}
            disabled={isSubmitting}
          />
          <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr 1fr 1fr', gap: '10px' }}>
            <Input
              label="City"
              value={city}
              onChange={(e) => setCity(e.target.value)}
              error={fieldErrors.city}
              disabled={isSubmitting}
            />
            <Input
              label="State"
              value={state}
              onChange={(e) => setState(e.target.value)}
              error={fieldErrors.state}
              disabled={isSubmitting}
            />
            <Input
              label="Postal Code"
              value={postalCode}
              onChange={(e) => setPostalCode(e.target.value)}
              error={fieldErrors.postalCode}
              disabled={isSubmitting}
            />
            <Input
              label="Country"
              value={country}
              onChange={(e) => setCountry(e.target.value)}
              error={fieldErrors.country}
              disabled={isSubmitting}
            />
          </div>
        </div>

        <PaymentSelectionForm
          selectedMethod={paymentMethod}
          onSelectMethod={setPaymentMethod}
          declineAlertReason={declineReason}
          onDismissDeclineAlert={() => setDeclineReason(null)}
          disabled={isSubmitting}
        />

        <div
          style={{
            padding: '14px',
            borderRadius: '10px',
            backgroundColor: '#f8fafc',
            border: '1px solid #e2e8f0',
          }}
        >
          <div
            style={{
              display: 'flex',
              justifyContent: 'space-between',
              alignItems: 'center',
              marginBottom: '10px',
            }}
          >
            <h3 style={{ margin: 0, fontSize: '15px', fontWeight: 700, color: '#0f172a' }}>
              Order Review Snapshot
            </h3>
            <Badge variant={hasValidationWarnings ? 'warning' : 'success'}>
              {hasValidationWarnings ? 'Price Adjusted to Live Catalog' : 'Prices Verified'}
            </Badge>
          </div>
          {quoteItems.length > 0 ? (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '6px' }}>
              {quoteItems.map((item, idx) => (
                <div
                  key={`${item.sku ?? idx}`}
                  style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    fontSize: '13px',
                    color: '#334155',
                  }}
                >
                  <span>
                    {item.productName ?? item.sku} × {item.quantity ?? 1}
                  </span>
                  <strong>
                    {item.currency ?? 'USD'} {(item.lineTotal ?? 0).toFixed(2)}
                  </strong>
                </div>
              ))}
              <div
                style={{
                  borderTop: '1px solid #cbd5e1',
                  paddingTop: '8px',
                  marginTop: '6px',
                  display: 'flex',
                  justifyContent: 'space-between',
                  fontSize: '16px',
                  fontWeight: 800,
                  color: '#0f172a',
                }}
              >
                <span>Verified Total</span>
                <span>
                  {quote?.currency ?? 'USD'} {(quote?.subtotal ?? 0).toFixed(2)}
                </span>
              </div>
            </div>
          ) : (
            <div style={{ fontSize: '13px', color: '#64748b' }}>
              Submitting current server-side cart snapshot for checkout saga execution.
            </div>
          )}
        </div>

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
          <Button type="button" variant="secondary" onClick={onClose} disabled={isSubmitting}>
            Back to Storefront
          </Button>
          <Button type="submit" variant="primary" isLoading={isSubmitting}>
            Place Order (Idempotent Saga)
          </Button>
        </div>
      </form>
    </Modal>
  );
};
