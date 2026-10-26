import React, { useCallback, useEffect, useState } from 'react';
import { ShoppingBag, Truck, ShieldCheck, RotateCcw, Sparkles, Package } from 'lucide-react';
import {
  CartDto,
  CartService,
  CheckoutQuoteDto,
  configureApiClients,
  InventoryService,
  OrderDto,
  ProductsService,
} from './api';
import { Button, ErrorBoundary, OfflineToast, ToastContainer } from './shared/components';
import { Product, ProductStatus, StockSummary, ToastMessage } from './shared/types';
import { ProductCatalogGrid } from './features/catalog';
import { ShoppingCartDrawer } from './features/cart';
import { CheckoutWizard } from './features/checkout';
import { CustomerOrderReceipt } from './features/orders';
import { AdminPortal } from './admin/AdminPortal';

const SEEDED_PRODUCTS: Product[] = [
  {
    id: 'prod-001',
    name: 'Ergonomic Mechanical Keyboard',
    description: 'Hot-swappable gasket-mounted mechanical keyboard with PBT keycaps.',
    sku: 'KB-ERG-01',
    price: 129.99,
    currency: 'USD',
    status: 'Active',
    priceVersion: 1,
  },
  {
    id: 'prod-002',
    name: 'Wireless Precision Mouse',
    description: 'Ultra-low latency 4KHz wireless optical sensor mouse (58g).',
    sku: 'MS-WLS-02',
    price: 79.99,
    currency: 'USD',
    status: 'Active',
    priceVersion: 2,
  },
  {
    id: 'prod-003',
    name: 'Studio 4K OLED Monitor',
    description: '27-inch 240Hz OLED display with USB-C 90W power delivery.',
    sku: 'MN-OLED-27',
    price: 649.0,
    currency: 'USD',
    status: 'Active',
    priceVersion: 4,
  },
  {
    id: 'prod-004',
    name: 'Thunderbolt 4 Pro Dock',
    description: '16-port aluminum docking station with dual 6K display output.',
    sku: 'DK-TB4-04',
    price: 219.5,
    currency: 'USD',
    status: 'Active',
    priceVersion: 1,
  },
  {
    id: 'prod-005',
    name: 'Legacy USB-C Braided Cable',
    description: 'First-generation braided charging cable.',
    sku: 'CB-USBC-03',
    price: 14.99,
    currency: 'USD',
    status: 'Discontinued',
    priceVersion: 1,
  },
];

const SEEDED_STOCK: Record<string, StockSummary> = {
  'KB-ERG-01': { sku: 'KB-ERG-01', availableQuantity: 24, reservedQuantity: 2 },
  'MS-WLS-02': { sku: 'MS-WLS-02', availableQuantity: 3, reservedQuantity: 4 },
  'MN-OLED-27': { sku: 'MN-OLED-27', availableQuantity: 11, reservedQuantity: 1 },
  'DK-TB4-04': { sku: 'DK-TB4-04', availableQuantity: 0, reservedQuantity: 5 },
  'CB-USBC-03': { sku: 'CB-USBC-03', availableQuantity: 0, reservedQuantity: 0 },
};

const INITIAL_DEMO_ORDER: OrderDto = {
  id: 'demo-ord-9042',
  customerId: 'cust-alex-01',
  status: 'Confirmed',
  totalAmount: 209.98,
  currency: 'USD',
  paymentMethod: 'credit_card',
  paymentAttemptId: 'pay-att-7812a',
  trackingNumber: 'SS-TRK-9042US',
  shippingAddress: {
    recipientName: 'Alex Rivera',
    street: '742 Evergreen Terrace, Suite 200',
    city: 'Austin',
    state: 'TX',
    postalCode: '78701',
    country: 'US',
    phone: '+1-512-555-0192',
  },
  items: [
    {
      id: 'item-001',
      sku: 'KB-ERG-01',
      productName: 'Ergonomic Mechanical Keyboard',
      quantity: 1,
      unitPrice: 129.99,
      lineTotal: 129.99,
    },
    {
      id: 'item-002',
      sku: 'MS-WLS-02',
      productName: 'Wireless Precision Mouse',
      quantity: 1,
      unitPrice: 79.99,
      lineTotal: 79.99,
    },
  ],
  createdAtUtc: new Date().toISOString(),
  updatedAtUtc: new Date().toISOString(),
};

function buildQuoteFromCart(cart: CartDto, catalog: Product[]): CheckoutQuoteDto {
  const bySku = new Map(catalog.map((p) => [p.sku, p] as const));
  const items = (cart.items ?? []).map((item) => {
    const sku = item.sku ?? '';
    const qty = item.quantity ?? 1;
    const prod = bySku.get(sku);
    const unitPrice = prod?.price ?? 0;
    return {
      sku,
      productName: prod?.name ?? sku,
      quantity: qty,
      unitPrice,
      lineTotal: Number((unitPrice * qty).toFixed(2)),
      currency: prod?.currency ?? 'USD',
      priceVersion: prod?.priceVersion ?? 1,
      isActive: prod ? prod.status === 'Active' : true,
    };
  });
  const subtotal = Number(items.reduce((acc, i) => acc + (i.lineTotal ?? 0), 0).toFixed(2));
  return {
    customerId: cart.customerId ?? 'cust-alex-01',
    items,
    subtotal,
    currency: 'USD',
    isValid: items.length > 0 && items.every((i) => i.isActive),
    validationErrors: [],
  };
}

export const App: React.FC = () => {
  const [products, setProducts] = useState<Product[]>(SEEDED_PRODUCTS);
  const [stockBySku, setStockBySku] = useState<Record<string, StockSummary>>(SEEDED_STOCK);
  const [isLoadingCatalog, setIsLoadingCatalog] = useState<boolean>(false);
  const [addingProductId, setAddingProductId] = useState<string | null>(null);

  const [cart, setCart] = useState<CartDto>({
    customerId: 'cust-alex-01',
    items: [{ sku: 'KB-ERG-01', quantity: 1 }],
    totalQuantity: 1,
  });
  const [quote, setQuote] = useState<CheckoutQuoteDto>(() =>
    buildQuoteFromCart(
      {
        customerId: 'cust-alex-01',
        items: [{ sku: 'KB-ERG-01', quantity: 1 }],
        totalQuantity: 1,
      },
      SEEDED_PRODUCTS
    )
  );
  const [isUpdatingCart, setIsUpdatingCart] = useState<boolean>(false);
  const [isCartOpen, setIsCartOpen] = useState<boolean>(false);
  const [isCheckoutOpen, setIsCheckoutOpen] = useState<boolean>(false);

  const [activeOrder, setActiveOrder] = useState<OrderDto>(INITIAL_DEMO_ORDER);
  const [showOrderTracker, setShowOrderTracker] = useState<boolean>(false);
  const [viewMode, setViewMode] = useState<'store' | 'admin'>('store');
  const [toasts, setToasts] = useState<ToastMessage[]>([]);

  const pushToast = useCallback(
    (
      title: string,
      description: string,
      variant: 'success' | 'warning' | 'danger' | 'info' = 'info'
    ) => {
      const id = `toast-${Date.now()}-${Math.random().toString(36).slice(2, 6)}`;
      setToasts((prev) => [...prev.slice(-3), { id, title, description, variant }]);
    },
    []
  );

  const dismissToast = useCallback((id: string) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  const refreshCatalogAndInventory = useCallback(async () => {
    setIsLoadingCatalog(true);
    try {
      const paged = await ProductsService.getCatalogProducts({
        page: 1,
        pageSize: 24,
        onlyActive: false,
      });
      if (paged.items && paged.items.length > 0) {
        const mapped: Product[] = paged.items.map((dto, index) => ({
          id: dto.id ?? `prod-${index}`,
          name: dto.name ?? 'Unnamed Product',
          description: dto.description ?? '',
          sku: dto.sku ?? `SKU-${index}`,
          price: dto.price ?? 0,
          currency: dto.currency ?? 'USD',
          status: (dto.status as ProductStatus) ?? 'Active',
          priceVersion: dto.priceVersion ?? 1,
        }));
        setProducts(mapped);

        const stockEntries = await Promise.all(
          mapped.map(async (p) => {
            try {
              const s = await InventoryService.getStockBySku({ sku: p.sku });
              return [
                p.sku,
                {
                  sku: p.sku,
                  availableQuantity: s.quantityAvailable ?? 0,
                  reservedQuantity: s.quantityHeld ?? 0,
                },
              ] as const;
            } catch {
              return [
                p.sku,
                SEEDED_STOCK[p.sku] ?? { sku: p.sku, availableQuantity: 10, reservedQuantity: 0 },
              ] as const;
            }
          })
        );
        setStockBySku(Object.fromEntries(stockEntries));
      }

      const serverCart = await CartService.getCart();
      if (!serverCart.items || serverCart.items.length === 0) {
        const defaultSku = paged.items?.[0]?.sku ?? 'KB-RGB-001';
        const seededCart = await CartService.addItemToCart({
          requestBody: { sku: defaultSku, quantity: 1 },
        });
        setCart(seededCart);
      } else {
        setCart(serverCart);
      }
      const serverQuote = await CartService.getCartCheckoutQuote();
      setQuote(serverQuote);
    } catch {
      // Gateway offline or running standalone UI demo: keep seeded catalog and stock
    } finally {
      setIsLoadingCatalog(false);
    }
  }, []);

  useEffect(() => {
    configureApiClients();
    void refreshCatalogAndInventory();
  }, [refreshCatalogAndInventory]);

  const handleAddToCart = async (product: Product) => {
    setAddingProductId(product.id);
    try {
      const updatedCart = await CartService.addItemToCart({
        requestBody: { sku: product.sku, quantity: 1 },
      });
      setCart(updatedCart);
      const freshQuote = await CartService.getCartCheckoutQuote();
      setQuote(freshQuote);
    } catch {
      // Fallback local cart update when Gateway is not running
      setCart((prev) => {
        const existingItems = [...(prev.items ?? [])];
        const idx = existingItems.findIndex((i) => i.sku === product.sku);
        if (idx >= 0) {
          existingItems[idx] = {
            ...existingItems[idx],
            quantity: (existingItems[idx].quantity ?? 1) + 1,
          };
        } else {
          existingItems.push({
            sku: product.sku,
            quantity: 1,
          });
        }
        const totalQuantity = existingItems.reduce((sum, i) => sum + (i.quantity ?? 0), 0);
        const nextCart: CartDto = {
          ...prev,
          items: existingItems,
          totalQuantity,
        };
        setQuote(buildQuoteFromCart(nextCart, products));
        return nextCart;
      });
    } finally {
      setAddingProductId(null);
      pushToast('Added to Cart', `${product.name} added to your shopping cart.`, 'success');
    }
  };

  const handleUpdateQuantity = async (sku: string, quantity: number) => {
    setIsUpdatingCart(true);
    try {
      const updated = await CartService.updateCartItemQuantity({
        sku,
        requestBody: { quantity },
      });
      setCart(updated);
      const freshQuote = await CartService.getCartCheckoutQuote();
      setQuote(freshQuote);
    } catch {
      setCart((prev) => {
        const items = (prev.items ?? []).map((item) =>
          item.sku === sku ? { ...item, quantity } : item
        );
        const totalQuantity = items.reduce((sum, i) => sum + (i.quantity ?? 0), 0);
        const nextCart: CartDto = { ...prev, items, totalQuantity };
        setQuote(buildQuoteFromCart(nextCart, products));
        return nextCart;
      });
    } finally {
      setIsUpdatingCart(false);
    }
  };

  const handleRemoveItem = async (sku: string) => {
    setIsUpdatingCart(true);
    try {
      await CartService.removeCartItem({ sku });
      const updated = await CartService.getCart();
      setCart(updated);
      const freshQuote = await CartService.getCartCheckoutQuote();
      setQuote(freshQuote);
    } catch {
      setCart((prev) => {
        const items = (prev.items ?? []).filter((item) => item.sku !== sku);
        const totalQuantity = items.reduce((sum, i) => sum + (i.quantity ?? 0), 0);
        const nextCart: CartDto = { ...prev, items, totalQuantity };
        setQuote(buildQuoteFromCart(nextCart, products));
        return nextCart;
      });
    } finally {
      setIsUpdatingCart(false);
    }
  };

  const handleClearCart = async () => {
    setIsUpdatingCart(true);
    try {
      await CartService.clearCart();
    } catch {
      // Fallback
    } finally {
      const emptyCart: CartDto = { customerId: 'cust-alex-01', items: [], totalQuantity: 0 };
      setCart(emptyCart);
      setQuote(buildQuoteFromCart(emptyCart, products));
      setIsUpdatingCart(false);
    }
  };

  const handleRefreshQuote = async () => {
    setIsUpdatingCart(true);
    try {
      const freshQuote = await CartService.getCartCheckoutQuote();
      setQuote(freshQuote);
    } catch {
      setQuote(buildQuoteFromCart(cart, products));
    } finally {
      setIsUpdatingCart(false);
      pushToast('Cart Updated', 'Your cart prices have been refreshed.', 'info');
    }
  };

  if (viewMode === 'admin') {
    return (
      <ErrorBoundary>
        <OfflineToast />
        <ToastContainer toasts={toasts} onDismiss={dismissToast} />
        <AdminPortal
          order={activeOrder}
          products={products}
          stockBySku={stockBySku}
          onOrderUpdated={setActiveOrder}
          onRefreshCatalog={() => void refreshCatalogAndInventory()}
          onBackToStore={() => setViewMode('store')}
          onNotifyToast={pushToast}
        />
      </ErrorBoundary>
    );
  }

  return (
    <ErrorBoundary>
      <div className="min-h-screen bg-gray-50 flex flex-col font-sans">
        <OfflineToast />
        <ToastContainer toasts={toasts} onDismiss={dismissToast} />

        {/* Storefront Header */}
        <header className="bg-white border-b border-gray-200 sticky top-0 z-30 shadow-xs">
          <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-16 flex items-center justify-between">
            {/* Brand Logo */}
            <div className="flex items-center space-x-3">
              <div className="w-9 h-9 rounded-xl bg-gradient-to-tr from-green-600 to-emerald-500 flex items-center justify-center text-white font-black text-xl shadow-xs">
                S
              </div>
              <div>
                <span className="text-xl font-extrabold tracking-tight text-gray-900 block leading-tight">
                  ShopSphere
                </span>
                <span className="text-[11px] font-medium text-gray-500 tracking-wide block uppercase">
                  Tech &amp; Workspace Gear
                </span>
              </div>
            </div>

            {/* Navigation links & Actions */}
            <div className="flex items-center space-x-2 sm:space-x-3">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setShowOrderTracker((prev) => !prev)}
                className="text-gray-700"
              >
                <Package className="w-4 h-4 mr-1.5 text-gray-500" />
                {showOrderTracker ? 'Hide Order' : 'Track Order'}
              </Button>

              <Button
                variant="outline"
                size="sm"
                onClick={async () => {
                  if (!cart.items || cart.items.length === 0) {
                    await handleAddToCart(products[0] ?? SEEDED_PRODUCTS[0]);
                  }
                  setIsCheckoutOpen(true);
                }}
              >
                Fast Checkout
              </Button>

              <Button
                variant="primary"
                size="sm"
                onClick={() => setIsCartOpen(true)}
                className="bg-green-600 hover:bg-green-500"
              >
                <ShoppingBag className="w-4 h-4 mr-1.5" />
                Cart ({cart.totalQuantity ?? 0})
              </Button>

              <div className="h-6 w-px bg-gray-200 mx-1 hidden sm:block" />

              <button
                type="button"
                onClick={() => setViewMode('admin')}
                className="inline-flex items-center space-x-1.5 px-3 py-1.5 rounded-lg border border-slate-300 bg-slate-100 hover:bg-slate-200 text-slate-800 text-xs font-bold transition-colors cursor-pointer"
                title="Switch to Staff Admin &amp; Operations Portal"
              >
                <span>⚙️ Staff Admin</span>
              </button>
            </div>
          </div>
        </header>

        {/* Main Storefront Body */}
        <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 flex-1 w-full space-y-8">
          {/* Hero Storefront Banner */}
          <section className="bg-gradient-to-r from-slate-900 via-emerald-950 to-slate-900 text-white rounded-3xl p-8 sm:p-12 shadow-sm relative overflow-hidden">
            <div className="max-w-2xl space-y-4 relative z-10">
              <div className="inline-flex items-center space-x-2 bg-emerald-500/20 text-emerald-300 border border-emerald-500/30 px-3 py-1 rounded-full text-xs font-semibold">
                <Sparkles className="w-3.5 h-3.5" />
                <span>Spring 2026 Hardware Drop</span>
              </div>
              <h1 className="text-3xl sm:text-5xl font-black tracking-tight leading-tight">
                Precision Hardware for Modern Desks
              </h1>
              <p className="text-sm sm:text-base text-gray-300 leading-relaxed font-normal">
                Elevate your focus with gasket-mounted keyboards, 4K OLED displays, and
                ultra-lightweight wireless mice. Guaranteed in-stock and covered by 1-year warranty.
              </p>

              {/* Value Props Badges */}
              <div className="pt-2 flex flex-wrap items-center gap-4 text-xs text-gray-300">
                <div className="flex items-center space-x-1.5">
                  <Truck className="w-4 h-4 text-emerald-400" />
                  <span>Free Express Delivery</span>
                </div>
                <div className="flex items-center space-x-1.5">
                  <ShieldCheck className="w-4 h-4 text-emerald-400" />
                  <span>1-Year Official Warranty</span>
                </div>
                <div className="flex items-center space-x-1.5">
                  <RotateCcw className="w-4 h-4 text-emerald-400" />
                  <span>30-Day Money-Back Guarantee</span>
                </div>
              </div>
            </div>
          </section>

          {/* Customer Order Receipt & Tracking (When active or opened) */}
          {showOrderTracker && (
            <CustomerOrderReceipt
              order={activeOrder}
              onOrderUpdated={setActiveOrder}
              onContinueShopping={() => setShowOrderTracker(false)}
              onViewStaffDetails={() => setViewMode('admin')}
            />
          )}

          {/* Product Catalog Grid */}
          <ProductCatalogGrid
            products={products}
            stockBySku={stockBySku}
            isLoading={isLoadingCatalog}
            onRefresh={() => void refreshCatalogAndInventory()}
            onAddToCart={(product) => void handleAddToCart(product)}
            addingProductId={addingProductId}
          />
        </main>

        {/* Cart Drawer */}
        <ShoppingCartDrawer
          isOpen={isCartOpen}
          onClose={() => setIsCartOpen(false)}
          cart={cart}
          quote={quote}
          isUpdating={isUpdatingCart}
          onUpdateQuantity={(sku, qty) => void handleUpdateQuantity(sku, qty)}
          onRemoveItem={(sku) => void handleRemoveItem(sku)}
          onClearCart={() => void handleClearCart()}
          onRefreshQuote={() => void handleRefreshQuote()}
          onProceedToCheckout={() => {
            setIsCartOpen(false);
            setIsCheckoutOpen(true);
          }}
        />

        {/* Checkout Wizard Modal */}
        <CheckoutWizard
          isOpen={isCheckoutOpen}
          quote={quote}
          onClose={() => setIsCheckoutOpen(false)}
          onOrderCreated={(createdOrder) => {
            setActiveOrder(createdOrder);
            setIsCheckoutOpen(false);
            setShowOrderTracker(true);
            const emptyCart: CartDto = { customerId: 'cust-alex-01', items: [], totalQuantity: 0 };
            setCart(emptyCart);
            setQuote(buildQuoteFromCart(emptyCart, products));
            pushToast(
              'Order Placed',
              `Thank you! Order #${createdOrder.id?.slice(0, 8) ?? ''} has been received.`,
              'success'
            );
          }}
        />

        {/* Customer Storefront Footer */}
        <footer className="bg-white border-t border-gray-200 py-8 px-4 text-center text-xs text-gray-500 space-y-2">
          <div className="flex items-center justify-center space-x-6 text-gray-600 font-medium">
            <span>Free Express Shipping</span>
            <span>&middot;</span>
            <span>30-Day Returns</span>
            <span>&middot;</span>
            <span>Stripe, MoMo &amp; VNPay</span>
            <span>&middot;</span>
            <button
              type="button"
              onClick={() => setViewMode('admin')}
              className="text-gray-500 hover:text-gray-900 underline"
            >
              Staff Portal
            </button>
          </div>
          <p className="text-gray-400">&copy; 2026 ShopSphere Inc. All rights reserved.</p>
        </footer>
      </div>
    </ErrorBoundary>
  );
};

export default App;
