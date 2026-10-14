import React, { useState } from 'react';
import { Button, OrderStatusBadge, Badge, Spinner } from './shared/components';
import { OrderState, Product } from './shared/types';

export const App: React.FC = () => {
  const [selectedState, setSelectedState] = useState<OrderState>('Pending');
  const [loading, setLoading] = useState<boolean>(false);

  const sampleProducts: Product[] = [
    {
      id: 'prod-001',
      name: 'Ergonomic Mechanical Keyboard',
      description: 'Hot-swappable switches with RGB backlight',
      sku: 'KB-ERG-01',
      price: 129.99,
      currency: 'USD',
      status: 'Active',
      priceVersion: 1,
    },
    {
      id: 'prod-002',
      name: 'Wireless Precision Mouse',
      description: 'Ultra-low latency wireless sensor',
      sku: 'MS-WLS-02',
      price: 79.99,
      currency: 'USD',
      status: 'Active',
      priceVersion: 2,
    },
    {
      id: 'prod-003',
      name: 'Legacy USB-C Cable',
      description: 'Braided fast charging cable',
      sku: 'CB-USBC-03',
      price: 14.99,
      currency: 'USD',
      status: 'Discontinued',
      priceVersion: 1,
    },
  ];

  const orderStates: OrderState[] = [
    'Pending',
    'Reserving',
    'Paying',
    'PaymentUnknown',
    'Confirmed',
    'Refunding',
    'Cancelled',
  ];

  const handleSimulateAction = (): void => {
    setLoading(true);
    setTimeout(() => {
      setLoading(false);
    }, 1000);
  };

  return (
    <div className="min-h-screen bg-gray-50 flex flex-col">
      {/* Header */}
      <header className="bg-white border-b border-gray-200">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-16 flex items-center justify-between">
          <div className="flex items-center space-x-3">
            <div className="w-8 h-8 rounded-lg bg-green-600 flex items-center justify-center text-white font-bold text-lg">
              S
            </div>
            <span className="text-xl font-bold tracking-tight text-gray-900">ShopSphere</span>
            <Badge variant="info">v0.1.0-alpha</Badge>
          </div>
          <div className="flex items-center space-x-4">
            <Button variant="outline" size="sm">
              Documentation
            </Button>
            <Button variant="primary" size="sm">
              Gateway Status
            </Button>
          </div>
        </div>
      </header>

      {/* Main Content */}
      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 flex-1 w-full space-y-8">
        {/* Architecture & State Showcase */}
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
          <h2 className="text-lg font-semibold text-gray-900 mb-2">Order Lifecycle States</h2>
          <p className="text-sm text-gray-500 mb-4">
            ShopSphere strictly models all order states including intermediate resilience states
            (PaymentUnknown, Reserving).
          </p>

          <div className="flex flex-wrap gap-2 mb-6">
            {orderStates.map((state) => (
              <button
                key={state}
                onClick={() => setSelectedState(state)}
                className={`transition-all ${
                  selectedState === state
                    ? 'scale-105 ring-2 ring-offset-2 ring-green-500'
                    : 'opacity-80 hover:opacity-100'
                }`}
              >
                <OrderStatusBadge status={state} />
              </button>
            ))}
          </div>

          <div className="p-4 bg-gray-50 rounded-lg border border-gray-200 flex items-center justify-between">
            <div>
              <span className="text-xs font-semibold uppercase text-gray-500">
                Active State Preview
              </span>
              <div className="mt-1 flex items-center space-x-3">
                <OrderStatusBadge status={selectedState} />
                <span className="text-sm text-gray-600">
                  Selected state for workflow inspection
                </span>
              </div>
            </div>
            <Button variant="primary" size="sm" isLoading={loading} onClick={handleSimulateAction}>
              Simulate Workflow Step
            </Button>
          </div>
        </div>

        {/* Catalog Preview */}
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
          <div className="flex items-center justify-between mb-4">
            <div>
              <h2 className="text-lg font-semibold text-gray-900">Catalog Service Preview</h2>
              <p className="text-sm text-gray-500">
                Products fetched via Gateway routing from CatalogService
              </p>
            </div>
            {loading && <Spinner size="sm" />}
          </div>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
            {sampleProducts.map((product) => (
              <div
                key={product.id}
                className="border border-gray-200 rounded-lg p-5 flex flex-col justify-between hover:shadow-md transition-shadow"
              >
                <div>
                  <div className="flex items-center justify-between mb-2">
                    <span className="text-xs font-mono text-gray-500">{product.sku}</span>
                    <Badge variant={product.status === 'Active' ? 'success' : 'default'}>
                      {product.status}
                    </Badge>
                  </div>
                  <h3 className="font-semibold text-gray-900">{product.name}</h3>
                  <p className="text-sm text-gray-600 mt-1">{product.description}</p>
                </div>
                <div className="mt-4 pt-4 border-t border-gray-100 flex items-center justify-between">
                  <div>
                    <span className="text-xs text-gray-400 block">
                      Price (v{product.priceVersion})
                    </span>
                    <span className="text-lg font-bold text-gray-900">
                      ${product.price.toFixed(2)} {product.currency}
                    </span>
                  </div>
                  <Button
                    variant={product.status === 'Active' ? 'primary' : 'outline'}
                    size="sm"
                    disabled={product.status !== 'Active'}
                  >
                    {product.status === 'Active' ? 'Add to Cart' : 'Unavailable'}
                  </Button>
                </div>
              </div>
            ))}
          </div>
        </div>
      </main>

      {/* Footer */}
      <footer className="bg-white border-t border-gray-200 py-4 text-center text-xs text-gray-400">
        ShopSphere Monorepo &middot; Microservice Architecture &middot; Strict TypeScript & Clean
        Architecture
      </footer>
    </div>
  );
};

export default App;
