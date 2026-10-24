import React from 'react';
import { ShoppingBag, Plus, Minus, Trash2, ArrowRight, X } from 'lucide-react';
import { Badge, Button } from '../../shared/components';
import type { CartDto, CheckoutQuoteDto } from '../../api';
import { PriceVerificationBanner } from './PriceVerificationBanner';

export interface ShoppingCartDrawerProps {
  isOpen: boolean;
  onClose: () => void;
  cart: CartDto | null;
  quote: CheckoutQuoteDto | null;
  isUpdating: boolean;
  onUpdateQuantity: (sku: string, quantity: number) => void;
  onRemoveItem: (sku: string) => void;
  onClearCart: () => void;
  onRefreshQuote: () => void;
  onProceedToCheckout: () => void;
}

export const ShoppingCartDrawer: React.FC<ShoppingCartDrawerProps> = ({
  isOpen,
  onClose,
  cart,
  quote,
  isUpdating,
  onUpdateQuantity,
  onRemoveItem,
  onClearCart,
  onRefreshQuote,
  onProceedToCheckout,
}) => {
  if (!isOpen) {
    return null;
  }

  const cartItems = cart?.items ?? [];
  const quoteItemsBySku = new Map(
    (quote?.items ?? []).map((item) => [item.sku ?? '', item] as const)
  );
  const canProceed = cartItems.length > 0 && quote?.isValid !== false;

  return (
    <div
      className="fixed inset-0 z-50 flex justify-end bg-black/40 backdrop-blur-xs"
      role="dialog"
      aria-modal="true"
      aria-label="Shopping Cart Drawer"
    >
      <div className="bg-white w-full max-w-md h-full shadow-2xl border-l border-gray-200 flex flex-col">
        <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between">
          <div className="flex items-center space-x-2.5">
            <ShoppingBag className="w-5 h-5 text-green-600" />
            <h3 className="text-base font-semibold text-gray-900">Your Shopping Cart</h3>
            <Badge variant="info">{cart?.totalQuantity ?? 0}</Badge>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 p-1 rounded-lg"
            aria-label="Close cart drawer"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto p-6 space-y-4">
          {cartItems.length === 0 ? (
            <div className="py-16 text-center space-y-2">
              <ShoppingBag className="w-10 h-10 text-gray-300 mx-auto" />
              <p className="text-sm font-medium text-gray-700">Your cart is empty</p>
              <p className="text-xs text-gray-500">
                Add items from the product catalog to generate a verified checkout quote.
              </p>
            </div>
          ) : (
            <>
              <PriceVerificationBanner
                quote={quote}
                isVerifying={isUpdating}
                onRefreshQuote={onRefreshQuote}
              />

              <div className="divide-y divide-gray-100 border border-gray-200 rounded-xl overflow-hidden">
                {cartItems.map((item) => {
                  const sku = item.sku ?? '';
                  const qty = item.quantity ?? 1;
                  const quoteItem = quoteItemsBySku.get(sku);

                  return (
                    <div key={sku} className="p-4 bg-white space-y-3">
                      <div className="flex items-start justify-between gap-2">
                        <div>
                          <div className="flex items-center space-x-2">
                            <span className="text-sm font-semibold text-gray-900">
                              {quoteItem?.productName ?? sku}
                            </span>
                            {quoteItem?.priceVersion !== undefined ? (
                              <Badge variant="default">v{quoteItem.priceVersion}</Badge>
                            ) : null}
                          </div>
                          <span className="text-xs font-mono text-gray-400">{sku}</span>
                        </div>
                        <div className="text-right">
                          <span className="text-sm font-bold text-gray-900 block">
                            ${(quoteItem?.lineTotal ?? 0).toFixed(2)}
                          </span>
                          <span className="text-xs text-gray-500">
                            ${(quoteItem?.unitPrice ?? 0).toFixed(2)} each
                          </span>
                        </div>
                      </div>

                      <div className="flex items-center justify-between">
                        <div className="inline-flex items-center rounded-lg border border-gray-300">
                          <button
                            type="button"
                            disabled={isUpdating || qty <= 1}
                            onClick={() => onUpdateQuantity(sku, qty - 1)}
                            className="p-1.5 text-gray-600 hover:bg-gray-50 disabled:opacity-40"
                            aria-label={`Decrease quantity for ${sku}`}
                          >
                            <Minus className="w-3.5 h-3.5" />
                          </button>
                          <span className="px-3 text-xs font-semibold text-gray-900">{qty}</span>
                          <button
                            type="button"
                            disabled={isUpdating}
                            onClick={() => onUpdateQuantity(sku, qty + 1)}
                            className="p-1.5 text-gray-600 hover:bg-gray-50 disabled:opacity-40"
                            aria-label={`Increase quantity for ${sku}`}
                          >
                            <Plus className="w-3.5 h-3.5" />
                          </button>
                        </div>

                        <button
                          type="button"
                          disabled={isUpdating}
                          onClick={() => onRemoveItem(sku)}
                          className="text-xs text-red-600 hover:text-red-700 inline-flex items-center space-x-1"
                        >
                          <Trash2 className="w-3.5 h-3.5" />
                          <span>Remove</span>
                        </button>
                      </div>
                    </div>
                  );
                })}
              </div>
            </>
          )}
        </div>

        <div className="p-6 bg-gray-50 border-t border-gray-200 space-y-3">
          <div className="flex items-center justify-between text-sm">
            <span className="font-medium text-gray-600">Verified Quote Total</span>
            <span className="text-lg font-bold text-gray-900">
              ${(quote?.subtotal ?? 0).toFixed(2)} {quote?.currency ?? 'USD'}
            </span>
          </div>

          <div className="flex items-center justify-between gap-3">
            <Button
              variant="outline"
              size="sm"
              disabled={cartItems.length === 0 || isUpdating}
              onClick={onClearCart}
            >
              Clear Cart
            </Button>
            <Button
              variant="primary"
              size="md"
              className="flex-1"
              disabled={!canProceed || isUpdating}
              onClick={onProceedToCheckout}
            >
              <span>Proceed to Checkout</span>
              <ArrowRight className="w-4 h-4 ml-1.5" />
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
};
