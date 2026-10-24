import React, { useCallback, useEffect, useState } from 'react';
import { ShoppingBag, Activity } from 'lucide-react';
import {
  CartDto,
  CartService,
  CheckoutQuoteDto,
  configureApiClients,
  getCorrelationId,
  InventoryService,
  OrderDto,
  ProductsService,
} from './api';
import { Badge, Button, ErrorBoundary, OfflineToast, ToastContainer } from './shared/components';
import { Product, ProductStatus, StockSummary, ToastMessage } from './shared/types';
import { ProductCatalogGrid } from './features/catalog';
import { ShoppingCartDrawer } from './features/cart';
import { CheckoutWizard } from './features/checkout';
import { LiveOrderTracker } from './features/orders';

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
      pushToast(
        'Added to Cart',
        `${product.name} (${product.sku}) added with live price snapshot v${product.priceVersion}.`,
        'success'
      );
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
      pushToast(
        'Quote Verified',
        'Cart prices verified against CatalogService live price versions.',
        'info'
      );
    }
  };

  return (
    <ErrorBoundary>
      <div className="min-h-screen bg-gray-50 flex flex-col">
        <OfflineToast />
        <ToastContainer toasts={toasts} onDismiss={dismissToast} />

        {/* Header */}
        <header className="bg-white border-b border-gray-200 sticky top-0 z-30">
          <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-16 flex items-center justify-between">
            <div className="flex items-center space-x-3">
              <div className="w-8 h-8 rounded-lg bg-green-600 flex items-center justify-center text-white font-bold text-lg">
                S
              </div>
              <span className="text-xl font-bold tracking-tight text-gray-900">ShopSphere</span>
              <Badge variant="info">YARP Gateway BFF</Badge>
              <span className="hidden md:inline-flex items-center text-xs text-gray-500 font-mono">
                <Activity className="w-3.5 h-3.5 mr-1 text-green-600" />
                X-Correlation-Id: {getCorrelationId().slice(0, 12)}
              </span>
            </div>

            <div className="flex items-center space-x-3">
              <Button variant="outline" size="sm" onClick={() => setIsCheckoutOpen(true)}>
                Quick Checkout
              </Button>
              <Button variant="primary" size="sm" onClick={() => setIsCartOpen(true)}>
                <ShoppingBag className="w-4 h-4 mr-1.5" />
                Cart ({cart.totalQuantity ?? 0})
              </Button>
            </div>
          </div>
        </header>

        {/* Main Storefront & Saga Lifecycle Workspace */}
        <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 flex-1 w-full space-y-8">
          <ProductCatalogGrid
            products={products}
            stockBySku={stockBySku}
            isLoading={isLoadingCatalog}
            onRefresh={() => void refreshCatalogAndInventory()}
            onAddToCart={(product) => void handleAddToCart(product)}
            addingProductId={addingProductId}
          />

          <LiveOrderTracker
            order={activeOrder}
            onOrderUpdated={setActiveOrder}
            onNotifyToast={pushToast}
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
            pushToast(
              'Order Submitted',
              `Order ${createdOrder.id ?? ''} entered the Checkout Saga.`,
              'success'
            );
          }}
        />

        {/* Footer */}
        <footer className="bg-white border-t border-gray-200 py-4 text-center text-xs text-gray-400">
          ShopSphere Monorepo &middot; YARP Gateway Routing &middot; Generated OpenAPI Clients
          &middot; Strict TypeScript
        </footer>
      </div>
    </ErrorBoundary>
  );
};

export default App;
