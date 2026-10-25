import React from 'react';
import { ShieldCheck, AlertTriangle, RefreshCw } from 'lucide-react';
import { Badge, Button } from '../../shared/components';
import type { CheckoutQuoteDto } from '../../api';

export interface PriceVerificationBannerProps {
  quote: CheckoutQuoteDto | null;
  isVerifying: boolean;
  onRefreshQuote: () => void;
}

export const PriceVerificationBanner: React.FC<PriceVerificationBannerProps> = ({
  quote,
  isVerifying,
  onRefreshQuote,
}) => {
  if (!quote) {
    return null;
  }

  const isValid = quote.isValid !== false;
  const errors = quote.validationErrors ?? [];
  const items = quote.items ?? [];

  return (
    <div
      className={`rounded-xl border p-4 space-y-3 ${
        isValid ? 'bg-emerald-50/70 border-emerald-200' : 'bg-amber-50 border-amber-300'
      }`}
    >
      <div className="flex items-start justify-between gap-2">
        <div className="flex items-start space-x-2.5">
          {isValid ? (
            <ShieldCheck className="w-5 h-5 text-emerald-600 shrink-0 mt-0.5" />
          ) : (
            <AlertTriangle className="w-5 h-5 text-amber-600 shrink-0 mt-0.5" />
          )}
          <div>
            <div className="flex items-center space-x-2">
              <span className="text-xs font-semibold uppercase tracking-wider text-gray-800">
                {isValid ? 'Best Price Guarantee' : 'Cart Requires Attention'}
              </span>
              <Badge variant={isValid ? 'success' : 'warning'}>
                {isValid ? 'Guaranteed' : 'Update Notice'}
              </Badge>
            </div>
            <p className="text-xs text-gray-600 mt-0.5">
              {isValid
                ? 'Your items and prices are locked in and reserved for checkout.'
                : 'One or more items in your cart had a price or availability update.'}
            </p>
          </div>
        </div>

        <Button
          variant="outline"
          size="sm"
          onClick={onRefreshQuote}
          disabled={isVerifying}
          aria-label="Refresh cart prices"
        >
          <RefreshCw className={`w-3.5 h-3.5 ${isVerifying ? 'animate-spin' : ''}`} />
        </Button>
      </div>

      {errors.length > 0 ? (
        <ul className="text-xs text-amber-800 list-disc list-inside space-y-1">
          {errors.map((err, idx) => (
            <li key={idx}>{err}</li>
          ))}
        </ul>
      ) : null}

      {items.length > 0 ? (
        <div className="pt-2 border-t border-emerald-200/60 flex items-center justify-between text-xs">
          <span className="text-gray-600">
            Subtotal ({items.reduce((acc, i) => acc + (i.quantity ?? 0), 0)} items)
          </span>
          <span className="text-sm font-bold text-gray-900">
            ${(quote.subtotal ?? 0).toFixed(2)} {quote.currency ?? 'USD'}
          </span>
        </div>
      ) : null}
    </div>
  );
};
