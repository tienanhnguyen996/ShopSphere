import React from 'react';
import { Tag, ShieldCheck, Layers, ShoppingCart } from 'lucide-react';
import { Badge, Button, Modal } from '../../shared/components';
import { Product, StockSummary } from '../../shared/types';
import { StockAvailabilityBadge } from './StockAvailabilityBadge';

export interface ProductDetailModalProps {
  product: Product | null;
  stock: StockSummary | undefined;
  isOpen: boolean;
  onClose: () => void;
  onAddToCart: (product: Product) => void;
  isAddingToCart?: boolean;
}

export const ProductDetailModal: React.FC<ProductDetailModalProps> = ({
  product,
  stock,
  isOpen,
  onClose,
  onAddToCart,
  isAddingToCart = false,
}) => {
  if (!product) {
    return null;
  }

  const availableQty = stock?.availableQuantity;
  const isPurchasable =
    product.status === 'Active' && (availableQty === undefined || availableQty > 0);

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title={product.name}
      maxWidthClassName="max-w-xl"
      footer={
        <>
          <Button variant="outline" size="sm" onClick={onClose}>
            Close
          </Button>
          <Button
            variant="primary"
            size="sm"
            disabled={!isPurchasable}
            isLoading={isAddingToCart}
            onClick={() => onAddToCart(product)}
          >
            <ShoppingCart className="w-4 h-4 mr-1.5" />
            {isPurchasable ? 'Add to Cart' : 'Unavailable'}
          </Button>
        </>
      }
    >
      <div className="space-y-5">
        <div className="flex flex-wrap items-center justify-between gap-2 pb-4 border-b border-gray-100">
          <div className="flex items-center space-x-2">
            <span className="text-xs font-mono bg-gray-100 text-gray-700 px-2.5 py-1 rounded-md">
              SKU: {product.sku}
            </span>
            <Badge variant={product.status === 'Active' ? 'success' : 'default'}>
              {product.status}
            </Badge>
          </div>
          <StockAvailabilityBadge availableQuantity={availableQty} />
        </div>

        <div>
          <h4 className="text-xs font-semibold uppercase tracking-wider text-gray-400 mb-1">
            Product Description
          </h4>
          <p className="text-sm text-gray-700 leading-relaxed">{product.description}</p>
        </div>

        <div className="bg-green-50/70 border border-green-200 rounded-xl p-4 flex items-center justify-between">
          <div>
            <div className="flex items-center space-x-2">
              <Tag className="w-4 h-4 text-green-700" />
              <span className="text-xs font-semibold uppercase text-green-800">
                Real-Time Catalog Price
              </span>
              <Badge variant="info">v{product.priceVersion}</Badge>
            </div>
            <p className="text-2xl font-bold text-gray-900 mt-1">
              ${product.price.toFixed(2)}{' '}
              <span className="text-sm font-normal text-gray-500">{product.currency}</span>
            </p>
          </div>
          <div className="text-right text-xs text-gray-600 space-y-1">
            <div className="flex items-center justify-end space-x-1 text-green-700 font-medium">
              <ShieldCheck className="w-4 h-4" />
              <span>Versioned Price Snapshot</span>
            </div>
            <p>Verified at checkout against Catalog v{product.priceVersion}</p>
          </div>
        </div>

        {stock ? (
          <div className="grid grid-cols-2 gap-3 text-xs">
            <div className="p-3 rounded-lg bg-gray-50 border border-gray-200 flex items-center justify-between">
              <span className="text-gray-500 flex items-center">
                <Layers className="w-3.5 h-3.5 mr-1.5 text-gray-400" />
                Available Stock
              </span>
              <span className="font-semibold text-gray-900">{stock.availableQuantity} units</span>
            </div>
            <div className="p-3 rounded-lg bg-gray-50 border border-gray-200 flex items-center justify-between">
              <span className="text-gray-500">Active Holds</span>
              <span className="font-semibold text-amber-700">{stock.reservedQuantity} held</span>
            </div>
          </div>
        ) : null}
      </div>
    </Modal>
  );
};
