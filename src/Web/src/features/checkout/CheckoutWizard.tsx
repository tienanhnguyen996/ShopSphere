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
          : 'Payment could not be processed. Please check your details and try again.';
      setDeclineReason(message);
      setIdempotencyKey(generateIdempotencyKey());
    } finally {
      setIsSubmitting(false);
    }
  };

  const quoteItems = quote?.items ?? [];
  const hasValidationWarnings = (quote?.validationErrors?.length ?? 0) > 0;

  return (
    <Modal isOpen={isOpen} title="Secure Checkout" onClose={onClose} maxWidthClassName="max-w-2xl">
      <form
        onSubmit={handleSubmitCheckout}
        style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}
      >
        <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <h3 style={{ margin: 0, fontSize: '16px', fontWeight: 700, color: '#0f172a' }}>
              1. Shipping Address
            </h3>
            <div style={{ display: 'flex', gap: '8px' }}>
              <button
                type="button"
                onClick={() => {
                  setRecipientName('Alex Rivera');
                  setStreet('742 Evergreen Terrace, Suite 200');
                  setCity('Austin');
                  setState('TX');
                  setPostalCode('78701');
                  setCountry('US');
                  setPhone('+1-512-555-0192');
                }}
                style={{
                  fontSize: '11px',
                  padding: '4px 10px',
                  borderRadius: '6px',
                  border: '1px solid #cbd5e1',
                  backgroundColor: country === 'US' ? '#e0f2fe' : '#ffffff',
                  color: country === 'US' ? '#0369a1' : '#475569',
                  cursor: 'pointer',
                  fontWeight: 600,
                }}
              >
                Autofill US Address
              </button>
              <button
                type="button"
                onClick={() => {
                  setRecipientName('Nguyen Van A');
                  setStreet('123 Le Loi, Ben Nghe');
                  setCity('Ho Chi Minh');
                  setState('Quan 1');
                  setPostalCode('70000');
                  setCountry('VN');
                  setPhone('0901234567');
                }}
                style={{
                  fontSize: '11px',
                  padding: '4px 10px',
                  borderRadius: '6px',
                  border: '1px solid #cbd5e1',
                  backgroundColor: country === 'VN' ? '#fef3c7' : '#ffffff',
                  color: country === 'VN' ? '#b45309' : '#475569',
                  cursor: 'pointer',
                  fontWeight: 600,
                }}
              >
                Autofill VN Address
              </button>
            </div>
          </div>
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
              Order Summary
            </h3>
            <Badge variant={hasValidationWarnings ? 'warning' : 'success'}>
              {hasValidationWarnings ? 'Price Updated' : 'Guaranteed Price'}
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
                <span>Total Due</span>
                <span>
                  {quote?.currency ?? 'USD'} {(quote?.subtotal ?? 0).toFixed(2)}
                </span>
              </div>
            </div>
          ) : (
            <div style={{ fontSize: '13px', color: '#64748b' }}>
              Your cart items are reserved for secure checkout.
            </div>
          )}
        </div>

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
          <Button type="button" variant="secondary" onClick={onClose} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button type="submit" variant="primary" isLoading={isSubmitting}>
            Place Order
          </Button>
        </div>
      </form>
    </Modal>
  );
};
