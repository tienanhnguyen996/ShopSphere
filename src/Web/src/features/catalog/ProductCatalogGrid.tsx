import React, { useMemo, useState } from 'react';
import { Search, Eye, ShoppingCart, RefreshCw } from 'lucide-react';
import { Button, Input, Spinner } from '../../shared/components';
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
  const [categoryFilter, setCategoryFilter] = useState<string>('ALL');
  const [selectedProduct, setSelectedProduct] = useState<Product | null>(null);

  const categories = useMemo(() => {
    return [
      { id: 'ALL', name: 'All Collection' },
      { id: 'KEYBOARD', name: 'Keyboards' },
      { id: 'MOUSE', name: 'Mice' },
      { id: 'DISPLAY', name: 'Displays' },
      { id: 'ACCESSORY', name: 'Accessories' },
    ];
  }, []);

  const getProductCategory = (sku: string, name: string): string => {
    const s = (sku + ' ' + name).toUpperCase();
    if (s.includes('KB') || s.includes('KEYBOARD')) return 'KEYBOARD';
    if (s.includes('MS') || s.includes('MOUSE')) return 'MOUSE';
    if (s.includes('MN') || s.includes('OLED') || s.includes('MONITOR')) return 'DISPLAY';
    return 'ACCESSORY';
  };

  const filteredProducts = useMemo(() => {
    const query = searchQuery.trim().toLowerCase();
    return products.filter((product) => {
      if (statusFilter !== 'ALL' && product.status !== statusFilter) {
        return false;
      }
      if (categoryFilter !== 'ALL') {
        const cat = getProductCategory(product.sku, product.name);
        if (cat !== categoryFilter) {
          return false;
        }
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
  }, [products, searchQuery, statusFilter, categoryFilter]);

  return (
    <section className="bg-white rounded-2xl shadow-xs border border-gray-100 p-6 sm:p-8 space-y-6">
      {/* Catalog Title & Search Bar */}
      <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4">
        <div>
          <h2 className="text-2xl font-bold tracking-tight text-gray-900">Featured Products</h2>
          <p className="text-sm text-gray-500 mt-1">
            Premium workspace hardware, guaranteed authentic and backed by our 30-day return policy.
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-3">
          <div className="relative w-full sm:w-64">
            <Input
              placeholder="Search products or keywords..."
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
            <option value="ALL">All Items</option>
            <option value="Active">In Stock &amp; Active</option>
            <option value="Discontinued">Archived</option>
          </select>

          <Button variant="outline" size="sm" onClick={onRefresh} disabled={isLoading}>
            <RefreshCw className={`w-3.5 h-3.5 mr-1.5 ${isLoading ? 'animate-spin' : ''}`} />
            Refresh
          </Button>
        </div>
      </div>

      {/* Category Filter Tabs */}
      <div className="flex items-center gap-2 overflow-x-auto pb-1 border-b border-gray-100">
        {categories.map((cat) => {
          const isActive = categoryFilter === cat.id;
          return (
            <button
              key={cat.id}
              type="button"
              onClick={() => setCategoryFilter(cat.id)}
              className={`px-4 py-2 rounded-lg text-xs font-semibold whitespace-nowrap transition-colors ${
                isActive
                  ? 'bg-gray-900 text-white shadow-xs'
                  : 'bg-gray-50 text-gray-600 hover:bg-gray-100 hover:text-gray-900'
              }`}
            >
              {cat.name}
            </button>
          );
        })}
      </div>

      {isLoading ? (
        <div className="py-16 flex flex-col items-center justify-center space-y-3">
          <Spinner size="md" />
          <span className="text-sm text-gray-500">Loading catalog...</span>
        </div>
      ) : filteredProducts.length === 0 ? (
        <div className="py-16 text-center text-sm text-gray-500 border border-dashed border-gray-200 rounded-xl">
          No products found matching your search. Try adjusting your query or category filters.
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {filteredProducts.map((product) => {
            const stock = stockBySku[product.sku];
            const availableQty = stock?.availableQuantity;
            const isOutOfStock = availableQty !== undefined && availableQty <= 0;
            const canAdd = product.status === 'Active' && !isOutOfStock;
            const category = getProductCategory(product.sku, product.name);

            return (
              <div
                key={product.id}
                className="group border border-gray-200/80 rounded-2xl p-5 flex flex-col justify-between hover:border-gray-300 hover:shadow-lg transition-all duration-200 bg-white"
              >
                <div className="space-y-3">
                  {/* Card Header */}
                  <div className="flex items-center justify-between gap-2">
                    <span className="text-[11px] font-semibold uppercase tracking-wider text-gray-400 bg-gray-50 px-2 py-0.5 rounded">
                      {category}
                    </span>
                    <StockAvailabilityBadge availableQuantity={availableQty} />
                  </div>

                  {/* Product Title */}
                  <div>
                    <h3 className="font-bold text-gray-900 text-base group-hover:text-green-600 transition-colors">
                      {product.name}
                    </h3>
                    <p className="text-xs text-gray-500 mt-1 line-clamp-2 leading-relaxed">
                      {product.description}
                    </p>
                  </div>
                </div>

                {/* Card Footer: Price and Add to Cart */}
                <div className="mt-5 pt-4 border-t border-gray-100 flex items-center justify-between">
                  <div>
                    <span className="text-xs text-gray-400 block font-medium">Price</span>
                    <span className="text-xl font-extrabold text-gray-900">
                      ${product.price.toFixed(2)}{' '}
                      <span className="text-xs font-normal text-gray-500">{product.currency}</span>
                    </span>
                  </div>

                  <div className="flex items-center space-x-2">
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => setSelectedProduct(product)}
                      aria-label={`View details for ${product.name}`}
                    >
                      <Eye className="w-3.5 h-3.5 mr-1" />
                      View
                    </Button>
                    <Button
                      variant={canAdd ? 'primary' : 'outline'}
                      size="sm"
                      disabled={!canAdd}
                      isLoading={addingProductId === product.id}
                      onClick={() => onAddToCart(product)}
                    >
                      <ShoppingCart className="w-3.5 h-3.5 mr-1" />
                      {isOutOfStock ? 'Sold Out' : 'Add to Cart'}
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
