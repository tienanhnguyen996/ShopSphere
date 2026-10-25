import React from 'react';
import { Tag, ShieldCheck, ShoppingCart } from 'lucide-react';
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

        <div className="bg-emerald-50/60 border border-emerald-200/80 rounded-xl p-4 flex items-center justify-between">
          <div>
            <div className="flex items-center space-x-1.5">
              <Tag className="w-4 h-4 text-emerald-700" />
              <span className="text-xs font-semibold uppercase text-emerald-800">Price</span>
            </div>
            <p className="text-3xl font-extrabold text-gray-900 mt-1">
              ${product.price.toFixed(2)}{' '}
              <span className="text-sm font-normal text-gray-500">{product.currency}</span>
            </p>
          </div>
          <div className="text-right text-xs text-emerald-800 space-y-1">
            <div className="flex items-center justify-end space-x-1 font-semibold">
              <ShieldCheck className="w-4 h-4 text-emerald-600" />
              <span>Authentic Guarantee</span>
            </div>
            <p className="text-emerald-700">Free shipping on orders over $50</p>
          </div>
        </div>

        <div className="grid grid-cols-2 gap-3 text-xs pt-1">
          <div className="p-3 rounded-xl bg-gray-50 border border-gray-100 flex items-center space-x-2">
            <span className="text-base">🚚</span>
            <div>
              <span className="font-semibold text-gray-900 block">Fast Dispatch</span>
              <span className="text-gray-500">Ships within 24-48 hours</span>
            </div>
          </div>
          <div className="p-3 rounded-xl bg-gray-50 border border-gray-100 flex items-center space-x-2">
            <span className="text-base">🛡️</span>
            <div>
              <span className="font-semibold text-gray-900 block">1-Year Warranty</span>
              <span className="text-gray-500">Official manufacturer cover</span>
            </div>
          </div>
        </div>
      </div>
    </Modal>
  );
};
