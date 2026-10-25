import React, { useMemo, useState } from 'react';
import { Search, Eye, ShoppingCart, RefreshCw } from 'lucide-react';
import { Badge, Button, Input, Spinner } from '../../shared/components';
import { Product, ProductStatus, StockSummary } from '../../shared/types';
import { StockAvailabilityBadge } from './StockAvailabilityBadge';
import { ProductDetailModal } from './ProductDetailModal';

export interface ProductCatalogGridProps {
  products: Product[];
  stockBySku: Record<string, StockSummary>;
  isLoading: boolean;
  onRefresh: () => void;
  onAddToCart: (product: Product) => void;
  addingProductId: string | null;
}

export const ProductCatalogGrid: React.FC<ProductCatalogGridProps> = ({
  products,
  stockBySku,
  isLoading,
  onRefresh,
  onAddToCart,
  addingProductId,
}) => {
  const [searchQuery, setSearchQuery] = useState<string>('');
  const [statusFilter, setStatusFilter] = useState<'ALL' | ProductStatus>('ALL');
  const [selectedProduct, setSelectedProduct] = useState<Product | null>(null);

  const filteredProducts = useMemo(() => {
    const query = searchQuery.trim().toLowerCase();
    return products.filter((product) => {
      if (statusFilter !== 'ALL' && product.status !== statusFilter) {
        return false;
      }
      if (!query) {
        return true;
      }
      return (
        product.name.toLowerCase().includes(query) ||
        product.sku.toLowerCase().includes(query) ||
        product.description.toLowerCase().includes(query)
      );
    });
  }, [products, searchQuery, statusFilter]);

  return (
    <section className="bg-white rounded-xl shadow-sm border border-gray-200 p-6 space-y-6">
      <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4">
        <div>
          <h2 className="text-lg font-semibold text-gray-900">Product Catalog</h2>
          <p className="text-xs text-gray-500">
            Routed through YARP Gateway (<code>/api/v1/products</code> &amp;{' '}
            <code>/api/v1/inventory</code>) with live stock indicators
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-3">
          <div className="relative w-64">
            <Input
              placeholder="Search name, SKU, or description..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              aria-label="Search catalog products"
            />
            <Search className="w-4 h-4 text-gray-400 absolute right-3 top-2.5 pointer-events-none" />
          </div>

          <select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value as 'ALL' | ProductStatus)}
            className="rounded-lg border border-gray-300 px-3 py-2 text-xs font-medium text-gray-700 focus:outline-none focus:ring-2 focus:ring-green-200"
            aria-label="Filter products by status"
          >
            <option value="ALL">All Statuses</option>
            <option value="Active">Active Only</option>
            <option value="Draft">Draft</option>
            <option value="Discontinued">Discontinued</option>
          </select>

          <Button variant="outline" size="sm" onClick={onRefresh} disabled={isLoading}>
            <RefreshCw className={`w-3.5 h-3.5 mr-1.5 ${isLoading ? 'animate-spin' : ''}`} />
            Refresh
          </Button>
        </div>
      </div>

      {isLoading ? (
        <div className="py-12 flex flex-col items-center justify-center space-y-2">
          <Spinner size="md" />
          <span className="text-xs text-gray-500">Loading catalog &amp; stock levels...</span>
        </div>
      ) : filteredProducts.length === 0 ? (
        <div className="py-10 text-center text-sm text-gray-500 border border-dashed border-gray-200 rounded-xl">
          No products match your current search or filter criteria.
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          {filteredProducts.map((product) => {
            const stock = stockBySku[product.sku];
            const availableQty = stock?.availableQuantity;
            const isOutOfStock = availableQty !== undefined && availableQty <= 0;
            const canAdd = product.status === 'Active' && !isOutOfStock;

            return (
              <div
                key={product.id}
                className="border border-gray-200 rounded-xl p-5 flex flex-col justify-between hover:shadow-md transition-shadow bg-white"
              >
                <div className="space-y-2.5">
                  <div className="flex items-center justify-between gap-2">
                    <span className="text-xs font-mono text-gray-500">{product.sku}</span>
                    <StockAvailabilityBadge availableQuantity={availableQty} />
                  </div>

                  <div className="flex items-center justify-between">
                    <h3 className="font-semibold text-gray-900">{product.name}</h3>
                    <Badge variant={product.status === 'Active' ? 'success' : 'default'}>
                      {product.status}
                    </Badge>
                  </div>

                  <p className="text-sm text-gray-600 line-clamp-2">{product.description}</p>
                </div>

                <div className="mt-5 pt-4 border-t border-gray-100 flex items-center justify-between">
                  <div>
                    <span className="text-xs text-gray-400 block">
                      Price Snapshot{' '}
                      <strong className="text-gray-600">v{product.priceVersion}</strong>
                    </span>
                    <span className="text-lg font-bold text-gray-900">
                      ${product.price.toFixed(2)} {product.currency}
                    </span>
                  </div>

                  <div className="flex items-center space-x-2">
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => setSelectedProduct(product)}
                      aria-label={`Inspect ${product.name}`}
                    >
                      <Eye className="w-3.5 h-3.5 mr-1" />
                      Details
                    </Button>
                    <Button
                      variant={canAdd ? 'primary' : 'outline'}
                      size="sm"
                      disabled={!canAdd}
                      isLoading={addingProductId === product.id}
                      onClick={() => onAddToCart(product)}
                    >
                      <ShoppingCart className="w-3.5 h-3.5 mr-1" />
                      {isOutOfStock
                        ? 'Out of Stock'
                        : product.status === 'Active'
                          ? 'Add'
                          : 'Unavailable'}
                    </Button>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}

      <ProductDetailModal
        product={selectedProduct}
        stock={selectedProduct ? stockBySku[selectedProduct.sku] : undefined}
        isOpen={selectedProduct !== null}
        onClose={() => setSelectedProduct(null)}
        onAddToCart={(product) => {
          onAddToCart(product);
          setSelectedProduct(null);
        }}
        isAddingToCart={selectedProduct !== null && addingProductId === selectedProduct.id}
      />
    </section>
  );
};
