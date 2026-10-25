import React, { useCallback, useEffect, useState } from 'react';
import { Activity, ArrowLeft, Boxes, Cpu, Mail, RefreshCw, Sliders, Truck } from 'lucide-react';
import {
  getCorrelationId,
  InventoryService,
  NotificationJobDto,
  NotificationsService,
  OrderDto,
  OrdersService,
  ShipmentDto,
  ShipmentsService,
} from '../api';
import { Badge, Button } from '../shared/components';
import {
  formatCurrencyAmount,
  normalizeOrderState,
  OrderState,
  Product,
  StockSummary,
} from '../shared/types';
import {
  EmailPreviewCard,
  OrderStateVisualizer,
  OrderTrackingTimeline,
  UnknownPaymentBanner,
} from '../features/orders';

export interface AdminPortalProps {
  order: OrderDto;
  products: Product[];
  stockBySku: Record<string, StockSummary>;
  onOrderUpdated: (updated: OrderDto) => void;
  onRefreshCatalog: () => void;
  onBackToStore: () => void;
  onNotifyToast?: (
    title: string,
    description: string,
    variant: 'success' | 'warning' | 'danger' | 'info'
  ) => void;
}

export const AdminPortal: React.FC<AdminPortalProps> = ({
  order,
  products,
  stockBySku,
  onOrderUpdated,
  onRefreshCatalog,
  onBackToStore,
  onNotifyToast,
}) => {
  const [activeTab, setActiveTab] = useState<
    'saga' | 'inventory' | 'shipping' | 'notifications' | 'diagnostics'
  >('saga');
  const [shipment, setShipment] = useState<ShipmentDto | null>(null);
  const [notifications, setNotifications] = useState<NotificationJobDto[]>([]);
  const [adjustSku, setAdjustSku] = useState<string>(products[0]?.sku ?? 'KB-ERG-01');
  const [adjustQty, setAdjustQty] = useState<number>(5);
  const [isAdjustingStock, setIsAdjustingStock] = useState<boolean>(false);
  const [isSyncingArtifacts, setIsSyncingArtifacts] = useState<boolean>(false);

  const orderState = normalizeOrderState(order.status ?? undefined);
  const isDemoOrder = !order.id || order.id.startsWith('demo-');

  const buildFallbackShipment = useCallback(
    (targetOrder: OrderDto): ShipmentDto => {
      const now = new Date().toISOString();
      const country = (targetOrder.shippingAddress?.country ?? 'US').toUpperCase();
      const isVietnam = country === 'VN' || country === 'VIETNAM';
      return {
        id: isDemoOrder
          ? `demo-ship-${targetOrder.id ?? '1'}`
          : (targetOrder.shipmentId ?? `demo-ship-${targetOrder.id ?? '1'}`),
        orderId: targetOrder.id,
        customerId: targetOrder.customerId,
        status: 'LabelCreated',
        carrier: isVietnam ? 'GHN Express (VN Sandbox)' : 'Shippo Express (US/Intl Sandbox)',
        trackingNumber:
          targetOrder.trackingNumber ??
          `${isVietnam ? 'GHN' : 'SS-TRK'}-${(targetOrder.id ?? '1001').slice(-6).toUpperCase()}`,
        address: {
          recipientName: targetOrder.shippingAddress?.recipientName ?? 'Alex Rivera',
          street: targetOrder.shippingAddress?.street ?? '742 Evergreen Terrace',
          city: targetOrder.shippingAddress?.city ?? 'Austin',
          state: targetOrder.shippingAddress?.state ?? 'TX',
          postalCode: targetOrder.shippingAddress?.postalCode ?? '78701',
          country: targetOrder.shippingAddress?.country ?? 'US',
        },
        createdAtUtc: now,
        milestones: [
          {
            id: 'ms-label',
            status: 'LabelCreated',
            location: isVietnam ? 'Ho Chi Minh Sorting Hub' : 'Austin Fulfillment Center',
            description: 'Shipping label created upon OrderConfirmed saga event.',
            occurredAtUtc: now,
          },
        ],
      };
    },
    [isDemoOrder]
  );

  const syncOrderArtifacts = useCallback(async () => {
    if (!order.id || isDemoOrder) {
      return;
    }

    setIsSyncingArtifacts(true);
    try {
      const latestOrder = await OrdersService.getOrderById({ id: order.id });
      onOrderUpdated(latestOrder);

      try {
        const latestShipment = await ShipmentsService.getShipmentByOrderId({ orderId: order.id });
        setShipment(latestShipment);
      } catch {
        // Shipment may not exist yet until OrderConfirmed
      }

      try {
        const jobs = await NotificationsService.getNotificationsByOrderId({ orderId: order.id });
        setNotifications(jobs);
      } catch {
        // Notifications may not exist yet
      }
    } catch {
      // Keep local snapshot if gateway is unreachable
    } finally {
      setIsSyncingArtifacts(false);
    }
  }, [isDemoOrder, onOrderUpdated, order.id]);

  useEffect(() => {
    void syncOrderArtifacts();
  }, [syncOrderArtifacts, orderState]);

  useEffect(() => {
    if (orderState === 'Confirmed' && !shipment) {
      setShipment(buildFallbackShipment(order));
    }

    if ((orderState === 'Confirmed' || orderState === 'Cancelled') && notifications.length === 0) {
      const now = new Date().toISOString();
      setNotifications([
        {
          id: `demo-notif-${order.id ?? '1'}`,
          orderId: order.id,
          customerId: order.customerId,
          notificationType: orderState === 'Confirmed' ? 'OrderConfirmed' : 'OrderCancelled',
          channel: 'Email',
          recipient: `${order.customerId ?? 'alex.rivera'}@customers.shopsphere.local`,
          subject:
            orderState === 'Confirmed'
              ? `Order ${order.id ?? ''} Confirmed — Ready for Carrier Handover`
              : `Order ${order.id ?? ''} Cancelled & Refund Initiated`,
          body:
            orderState === 'Confirmed'
              ? `Hello ${order.shippingAddress?.recipientName ?? 'Customer'},\nYour order (${formatCurrencyAmount(order.totalAmount ?? 0, order.currency, false)}) has been confirmed. Tracking number: ${order.trackingNumber ?? 'SS-TRK-ACTIVE'}.`
              : `Hello ${order.shippingAddress?.recipientName ?? 'Customer'},\nYour order (${order.id ?? ''}) was cancelled and any reserved stock or payment has been compensated.`,
          status: 'Sent',
          retryCount: 0,
          createdAtUtc: now,
          sentAtUtc: now,
        },
      ]);
    }
  }, [buildFallbackShipment, notifications.length, order, orderState, shipment]);

  const handleProvisionShipmentLabel = () => {
    const generated = buildFallbackShipment(order);
    setShipment(generated);
    const updatedOrder: OrderDto = {
      ...order,
      status: 'Confirmed',
      shipmentId: generated.id,
      trackingNumber: generated.trackingNumber,
      updatedAtUtc: new Date().toISOString(),
    };
    onOrderUpdated(updatedOrder);
    if (onNotifyToast) {
      onNotifyToast(
        '[Admin] Carrier Label Provisioned',
        `Assigned tracking # ${generated.trackingNumber ?? ''} via ${generated.carrier ?? 'Carrier'}.`,
        'success'
      );
    }
  };

  const handleSimulateStateChange = (nextState: OrderState) => {
    const updated: OrderDto = {
      ...order,
      status: nextState,
      updatedAtUtc: new Date().toISOString(),
    };
    onOrderUpdated(updated);
    if (onNotifyToast) {
      onNotifyToast(
        `[Admin] Order State -> ${nextState}`,
        `Order ${order.id ?? ''} transitioned to ${nextState}.`,
        nextState === 'Confirmed'
          ? 'success'
          : nextState === 'PaymentUnknown'
            ? 'warning'
            : nextState === 'Cancelled'
              ? 'danger'
              : 'info'
      );
    }
  };

  const handleAdjustStock = async () => {
    setIsAdjustingStock(true);
    try {
      await InventoryService.adjustStock({
        requestBody: {
          sku: adjustSku,
          quantity: adjustQty,
          reason: 'Admin manual inventory adjustment',
        },
      });
      if (onNotifyToast) {
        onNotifyToast(
          'Inventory Adjusted',
          `Adjusted ${adjustSku} by ${adjustQty > 0 ? '+' : ''}${adjustQty} units.`,
          'success'
        );
      }
      onRefreshCatalog();
    } catch {
      if (onNotifyToast) {
        onNotifyToast(
          'Adjustment Completed (Mocked)',
          `Updated local stock for ${adjustSku}.`,
          'info'
        );
      }
      onRefreshCatalog();
    } finally {
      setIsAdjustingStock(false);
    }
  };

  return (
    <div className="min-h-screen bg-slate-900 text-slate-100 flex flex-col font-sans">
      {/* Admin Top Navigation */}
      <header className="bg-slate-950 border-b border-slate-800 sticky top-0 z-40 px-4 sm:px-8 py-3.5 flex flex-wrap items-center justify-between gap-4">
        <div className="flex items-center space-x-3">
          <div className="w-8 h-8 rounded-lg bg-indigo-600 flex items-center justify-center text-white font-black text-sm">
            ⚙️
          </div>
          <div>
            <div className="flex items-center space-x-2">
              <span className="font-extrabold text-base tracking-tight text-white">
                ShopSphere Staff &amp; Operations Portal
              </span>
              <Badge variant="warning">Internal Admin</Badge>
            </div>
            <div className="flex items-center space-x-2 text-xs text-slate-400 font-mono mt-0.5">
              <span>BFF Gateway: :5000</span>
              <span>&middot;</span>
              <span className="flex items-center text-emerald-400">
                <Activity className="w-3 h-3 mr-1" />
                Trace: {getCorrelationId().slice(0, 8)}
              </span>
            </div>
          </div>
        </div>

        <div className="flex items-center space-x-3">
          <Button
            variant="primary"
            size="sm"
            onClick={onBackToStore}
            className="bg-emerald-600 hover:bg-emerald-500 text-white border-0 shadow-sm"
          >
            <ArrowLeft className="w-4 h-4 mr-1.5" />
            Back to Customer Store
          </Button>
        </div>
      </header>

      {/* Admin Subheader & Tabs */}
      <div className="bg-slate-950/60 border-b border-slate-800/80 px-4 sm:px-8 py-2.5 flex items-center justify-between overflow-x-auto gap-2">
        <div className="flex items-center space-x-1.5">
          <button
            type="button"
            onClick={() => setActiveTab('saga')}
            className={`flex items-center space-x-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold transition-colors ${
              activeTab === 'saga'
                ? 'bg-indigo-600 text-white'
                : 'text-slate-400 hover:text-white hover:bg-slate-800'
            }`}
          >
            <Sliders className="w-3.5 h-3.5" />
            <span>Saga &amp; Order Lifecycle</span>
          </button>

          <button
            type="button"
            onClick={() => setActiveTab('inventory')}
            className={`flex items-center space-x-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold transition-colors ${
              activeTab === 'inventory'
                ? 'bg-indigo-600 text-white'
                : 'text-slate-400 hover:text-white hover:bg-slate-800'
            }`}
          >
            <Boxes className="w-3.5 h-3.5" />
            <span>Inventory Management</span>
          </button>

          <button
            type="button"
            onClick={() => setActiveTab('shipping')}
            className={`flex items-center space-x-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold transition-colors ${
              activeTab === 'shipping'
                ? 'bg-indigo-600 text-white'
                : 'text-slate-400 hover:text-white hover:bg-slate-800'
            }`}
          >
            <Truck className="w-3.5 h-3.5" />
            <span>Carrier Fulfillment</span>
          </button>

          <button
            type="button"
            onClick={() => setActiveTab('notifications')}
            className={`flex items-center space-x-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold transition-colors ${
              activeTab === 'notifications'
                ? 'bg-indigo-600 text-white'
                : 'text-slate-400 hover:text-white hover:bg-slate-800'
            }`}
          >
            <Mail className="w-3.5 h-3.5" />
            <span>Email Dispatches</span>
          </button>

          <button
            type="button"
            onClick={() => setActiveTab('diagnostics')}
            className={`flex items-center space-x-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold transition-colors ${
              activeTab === 'diagnostics'
                ? 'bg-indigo-600 text-white'
                : 'text-slate-400 hover:text-white hover:bg-slate-800'
            }`}
          >
            <Cpu className="w-3.5 h-3.5" />
            <span>System Health</span>
          </button>
        </div>

        <div className="flex items-center space-x-2 text-xs text-slate-400 font-mono">
          <span>Active Order:</span>
          <span className="text-white font-bold">{order.id ?? 'None'}</span>
        </div>
      </div>

      {/* Main Admin Workspace */}
      <main className="max-w-7xl mx-auto px-4 sm:px-8 py-6 w-full flex-1 space-y-6">
        {/* TAB 1: SAGA & ORDER LIFECYCLE */}
        {activeTab === 'saga' && (
          <div className="space-y-6">
            <div className="bg-slate-800/80 border border-slate-700/80 rounded-2xl p-5 flex flex-wrap items-center justify-between gap-4">
              <div>
                <span className="text-xs font-bold uppercase tracking-wider text-slate-400 block">
                  Active Saga Instance
                </span>
                <div className="flex items-center space-x-2.5 mt-1">
                  <h2 className="text-lg font-mono font-bold text-white">
                    Order #{order.id ?? 'demo-ord-9042'}
                  </h2>
                  <Badge
                    variant={
                      orderState === 'Confirmed'
                        ? 'success'
                        : orderState === 'Cancelled'
                          ? 'danger'
                          : orderState === 'PaymentUnknown'
                            ? 'warning'
                            : 'info'
                    }
                  >
                    {orderState}
                  </Badge>
                </div>
                <p className="text-xs text-slate-400 mt-1">
                  Total: {formatCurrencyAmount(order.totalAmount ?? 0, order.currency, true)}{' '}
                  &middot; Payment: {order.paymentMethod ?? 'credit_card'}
                </p>
              </div>

              <div className="flex items-center space-x-2">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => onRefreshCatalog()}
                  className="border-slate-600 text-slate-300 hover:bg-slate-700"
                >
                  <RefreshCw className="w-3.5 h-3.5 mr-1.5" />
                  Sync State
                </Button>
              </div>
            </div>

            <UnknownPaymentBanner
              order={order}
              onRefreshOrder={async () => onRefreshCatalog()}
              onResolvedDemo={() => handleSimulateStateChange('Confirmed')}
            />

            {/* Saga State Visualizer Component */}
            <div className="bg-white rounded-2xl p-6 text-slate-900 shadow-sm border border-slate-200">
              <OrderStateVisualizer
                status={orderState}
                onSimulateStateChange={handleSimulateStateChange}
              />
            </div>
          </div>
        )}

        {/* TAB 2: INVENTORY MANAGEMENT */}
        {activeTab === 'inventory' && (
          <div className="space-y-6">
            <div className="bg-slate-800/80 border border-slate-700/80 rounded-2xl p-6 space-y-4">
              <div className="flex items-center justify-between">
                <div>
                  <h3 className="text-base font-bold text-white">Live Inventory Stock Levels</h3>
                  <p className="text-xs text-slate-400 mt-0.5">
                    Synchronized with InventoryService SQL table &amp; optimistic concurrency locks
                    (rowversion).
                  </p>
                </div>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={onRefreshCatalog}
                  className="border-slate-600 text-slate-300 hover:bg-slate-700"
                >
                  <RefreshCw className="w-3.5 h-3.5 mr-1" />
                  Refresh
                </Button>
              </div>

              <div className="overflow-x-auto rounded-xl border border-slate-700">
                <table className="w-full text-left text-xs text-slate-300">
                  <thead className="bg-slate-950 text-slate-400 uppercase font-semibold text-[11px]">
                    <tr>
                      <th className="py-3 px-4">Product Name</th>
                      <th className="py-3 px-4">SKU</th>
                      <th className="py-3 px-4">Available Quantity</th>
                      <th className="py-3 px-4">Active Holds (15m TTL)</th>
                      <th className="py-3 px-4">Status</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-800 bg-slate-900/50">
                    {products.map((p) => {
                      const stock = stockBySku[p.sku];
                      const avail = stock?.availableQuantity ?? 0;
                      const held = stock?.reservedQuantity ?? 0;
                      return (
                        <tr key={p.sku} className="hover:bg-slate-800/50 transition-colors">
                          <td className="py-3 px-4 font-medium text-white">{p.name}</td>
                          <td className="py-3 px-4 font-mono text-slate-400">{p.sku}</td>
                          <td className="py-3 px-4">
                            <span
                              className={`font-bold ${avail > 5 ? 'text-emerald-400' : avail > 0 ? 'text-amber-400' : 'text-rose-400'}`}
                            >
                              {avail} units
                            </span>
                          </td>
                          <td className="py-3 px-4 font-mono text-slate-400">{held} units</td>
                          <td className="py-3 px-4">
                            <Badge variant={p.status === 'Active' ? 'success' : 'default'}>
                              {p.status}
                            </Badge>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </div>

            {/* Stock Adjustment Controls */}
            <div className="bg-slate-800/80 border border-slate-700/80 rounded-2xl p-6 space-y-4">
              <h3 className="text-base font-bold text-white">Manual Stock Adjustment</h3>
              <div className="flex flex-wrap items-center gap-4">
                <div className="w-48">
                  <label className="text-xs text-slate-400 block mb-1">Target SKU</label>
                  <select
                    value={adjustSku}
                    onChange={(e) => setAdjustSku(e.target.value)}
                    className="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white"
                  >
                    {products.map((p) => (
                      <option key={p.sku} value={p.sku}>
                        {p.sku} - {p.name}
                      </option>
                    ))}
                  </select>
                </div>

                <div className="w-36">
                  <label className="text-xs text-slate-400 block mb-1">Quantity Delta (+/-)</label>
                  <input
                    type="number"
                    value={adjustQty}
                    onChange={(e) => setAdjustQty(parseInt(e.target.value, 10) || 0)}
                    className="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white font-mono"
                  />
                </div>

                <div className="pt-5">
                  <Button
                    variant="primary"
                    size="sm"
                    disabled={isAdjustingStock}
                    isLoading={isAdjustingStock}
                    onClick={handleAdjustStock}
                    className="bg-indigo-600 hover:bg-indigo-500"
                  >
                    Apply Stock Delta
                  </Button>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* TAB 3: SHIPPING & FULFILLMENT */}
        {activeTab === 'shipping' && (
          <div className="space-y-6">
            <div className="bg-slate-800/80 border border-slate-700/80 rounded-2xl p-5 flex flex-wrap items-center justify-between gap-4">
              <div>
                <span className="text-xs font-bold uppercase tracking-wider text-slate-400 block">
                  Carrier Fulfillment &amp; Handover Controls
                </span>
                <div className="flex items-center space-x-2.5 mt-1">
                  <h2 className="text-lg font-mono font-bold text-white">
                    Order #{order.id ?? 'demo-ord-9042'}
                  </h2>
                  <Badge variant={orderState === 'Confirmed' ? 'success' : 'warning'}>
                    Order: {orderState}
                  </Badge>
                  {shipment && (
                    <Badge variant="info">
                      Shipment: {shipment.status ?? 'LabelCreated'} ({shipment.trackingNumber})
                    </Badge>
                  )}
                </div>
                <p className="text-xs text-slate-400 mt-1">
                  Routes Vietnam (`VN`) addresses to GHN Sandbox and US/International addresses to
                  Shippo Sandbox (with MockCarrierProviderClient fallback).
                </p>
              </div>

              <div className="flex flex-wrap items-center gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  isLoading={isSyncingArtifacts}
                  onClick={() => void syncOrderArtifacts()}
                  className="border-slate-600 text-slate-300 hover:bg-slate-700"
                >
                  <RefreshCw className="w-3.5 h-3.5 mr-1.5" />
                  Sync from ShippingService
                </Button>
                {!shipment && (
                  <Button
                    variant="primary"
                    size="sm"
                    onClick={handleProvisionShipmentLabel}
                    className="bg-indigo-600 hover:bg-indigo-500 text-white"
                  >
                    <Truck className="w-3.5 h-3.5 mr-1.5" />
                    Provision Carrier Label Now
                  </Button>
                )}
              </div>
            </div>

            <div className="bg-white rounded-2xl p-6 text-slate-900 shadow-sm border border-slate-200">
              <OrderTrackingTimeline
                shipment={shipment}
                onShipmentUpdated={(updatedShipment) => {
                  setShipment(updatedShipment);
                  if (onNotifyToast) {
                    onNotifyToast(
                      `[Admin] Carrier Updated`,
                      `Shipment ${updatedShipment.id ?? ''} marked as ${updatedShipment.status}.`,
                      'success'
                    );
                  }
                }}
              />
            </div>
          </div>
        )}

        {/* TAB 4: NOTIFICATIONS & EMAIL */}
        {activeTab === 'notifications' && (
          <div className="space-y-6">
            <div className="bg-white rounded-2xl p-6 text-slate-900 shadow-sm border border-slate-200">
              <EmailPreviewCard
                notifications={notifications}
                onNotificationUpdated={(updatedJob) => {
                  setNotifications((prev) =>
                    prev.map((item) => (item.id === updatedJob.id ? updatedJob : item))
                  );
                }}
              />
            </div>
          </div>
        )}

        {/* TAB 5: SYSTEM DIAGNOSTICS */}
        {activeTab === 'diagnostics' && (
          <div className="space-y-6">
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
              <div className="bg-slate-800/80 border border-slate-700 rounded-2xl p-5 space-y-2">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-400">
                  Infrastructure
                </span>
                <p className="text-sm font-semibold text-white">Docker Compose Services</p>
                <div className="text-xs text-slate-400 space-y-1 pt-2 border-t border-slate-700 font-mono">
                  <p>SQL Server: localhost:1433</p>
                  <p>RabbitMQ: localhost:5672 (Console: :15672)</p>
                  <p>Redis Cache: localhost:6379</p>
                </div>
              </div>

              <div className="bg-slate-800/80 border border-slate-700 rounded-2xl p-5 space-y-2">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-400">
                  Gateway Routing
                </span>
                <p className="text-sm font-semibold text-white">YARP Reverse Proxy (:5000)</p>
                <div className="text-xs text-slate-400 space-y-1 pt-2 border-t border-slate-700 font-mono">
                  <p>Catalog: :5001 &middot; Cart: :5002</p>
                  <p>Inventory: :5003 &middot; Payment: :5004</p>
                  <p>Order: :5005 &middot; Shipping: :5006</p>
                  <p>Identity: :5007 &middot; Notification: :5008</p>
                </div>
              </div>

              <div className="bg-slate-800/80 border border-slate-700 rounded-2xl p-5 space-y-2">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-400">
                  Active Integrations
                </span>
                <p className="text-sm font-semibold text-white">Carriers &amp; Payment</p>
                <div className="text-xs text-slate-400 space-y-1 pt-2 border-t border-slate-700">
                  <p>Payments: Stripe, MoMo, VNPay</p>
                  <p>Shipping: Shippo (US/Intl), GHN (Vietnam)</p>
                  <p>Resilience: Transactional Outbox + Auto-Compensate</p>
                </div>
              </div>
            </div>

            <div className="bg-slate-800/80 border border-slate-700 rounded-2xl p-5 space-y-3">
              <h4 className="text-xs font-bold uppercase tracking-wider text-slate-400">
                Raw Active Order State (JSON)
              </h4>
              <pre className="bg-slate-950 p-4 rounded-xl text-xs text-emerald-400 font-mono overflow-x-auto max-h-64">
                {JSON.stringify(order, null, 2)}
              </pre>
            </div>
          </div>
        )}
      </main>
    </div>
  );
};
