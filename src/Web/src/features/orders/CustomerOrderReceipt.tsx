import React, { useCallback, useEffect, useRef, useState } from 'react';
import {
  CheckCircle2,
  Clock,
  Package,
  Truck,
  AlertCircle,
  Copy,
  Check,
  ArrowRight,
  RefreshCw,
} from 'lucide-react';
import { OrderDto, OrdersService, ShipmentDto, ShipmentsService } from '../../api';
import { Badge, Button } from '../../shared/components';
import {
  formatCurrencyAmount,
  normalizeOrderState,
  normalizeShipmentState,
} from '../../shared/types';

export interface CustomerOrderReceiptProps {
  order: OrderDto;
  orders?: OrderDto[];
  shipment?: ShipmentDto | null;
  onSelectOrder?: (order: OrderDto) => void;
  onOrderUpdated?: (order: OrderDto) => void;
  onContinueShopping: () => void;
  onViewStaffDetails?: () => void;
}

export const CustomerOrderReceipt: React.FC<CustomerOrderReceiptProps> = ({
  order,
  orders,
  shipment,
  onSelectOrder,
  onOrderUpdated,
  onContinueShopping,
  onViewStaffDetails,
}) => {
  const [copiedId, setCopiedId] = useState(false);
  const [isCancelling, setIsCancelling] = useState(false);
  const [fetchedShipment, setFetchedShipment] = useState<ShipmentDto | null>(null);
  const [fetchedOrders, setFetchedOrders] = useState<OrderDto[]>([]);
  const [isRefreshing, setIsRefreshing] = useState(false);

  const allOrders =
    fetchedOrders.length > 0 ? fetchedOrders : orders && orders.length > 0 ? orders : [order];
  const resolvedShipment = shipment ?? fetchedShipment;
  const orderState = normalizeOrderState(order.status ?? undefined);
  const shipmentState = resolvedShipment
    ? normalizeShipmentState(resolvedShipment.status ?? undefined)
    : null;

  // Keep latest callback/order in refs so polling callbacks stay referentially stable.
  // Depending on an inline parent callback here previously caused an infinite
  // fetch -> setState -> re-render -> refetch loop that exhausted the gateway rate limit (429).
  const onOrderUpdatedRef = useRef(onOrderUpdated);
  const orderRef = useRef(order);
  const inFlightRef = useRef(false);
  useEffect(() => {
    onOrderUpdatedRef.current = onOrderUpdated;
    orderRef.current = order;
  });

  const syncOrderList = useCallback(async () => {
    try {
      const pagedOrders = await OrdersService.getOrders({ page: 1, pageSize: 20 });
      if (pagedOrders.items && pagedOrders.items.length > 0) {
        setFetchedOrders(pagedOrders.items);
      }
    } catch {
      // Keep current orders list if OrderService fails
    }
  }, []);

  const syncOrderAndShipment = useCallback(async (orderId: string | null | undefined) => {
    if (!orderId || orderId.startsWith('demo-') || inFlightRef.current) {
      return;
    }
    inFlightRef.current = true;
    setIsRefreshing(true);
    try {
      try {
        const latestOrder = await OrdersService.getOrderById({ id: orderId });
        const current = orderRef.current;
        const hasChanged =
          current.id !== latestOrder.id ||
          current.status !== latestOrder.status ||
          current.updatedAtUtc !== latestOrder.updatedAtUtc ||
          current.trackingNumber !== latestOrder.trackingNumber;
        if (hasChanged && onOrderUpdatedRef.current) {
          onOrderUpdatedRef.current(latestOrder);
        }
      } catch {
        // Keep current order snapshot if OrderService fails (e.g. 429 / offline)
      }

      try {
        const latestShipment = await ShipmentsService.getShipmentByOrderId({ orderId });
        setFetchedShipment(latestShipment);
      } catch {
        // Shipment may not exist yet before OrderConfirmed; keep last known snapshot
      }
    } finally {
      inFlightRef.current = false;
      setIsRefreshing(false);
    }
  }, []);

  // Load the customer's order list once when the panel opens
  useEffect(() => {
    void syncOrderList();
  }, [syncOrderList]);

  // Sync the selected order + shipment when the selected order changes
  useEffect(() => {
    void syncOrderAndShipment(order.id);
  }, [order.id, syncOrderAndShipment]);

  // Poll every 5s while the order is still progressing and the tab is visible
  useEffect(() => {
    if (!order.id || order.id.startsWith('demo-')) {
      return;
    }
    if (orderState === 'Cancelled' || shipmentState === 'Delivered') {
      return;
    }
    const timer = window.setInterval(() => {
      if (document.visibilityState === 'visible') {
        void syncOrderAndShipment(order.id);
      }
    }, 5000);
    return () => window.clearInterval(timer);
  }, [order.id, orderState, shipmentState, syncOrderAndShipment]);

  const handleCopyOrderId = () => {
    if (order.id) {
      void navigator.clipboard.writeText(order.id);
      setCopiedId(true);
      setTimeout(() => setCopiedId(false), 2000);
    }
  };

  const handleCancelOrder = async () => {
    if (!order.id) return;
    if (!window.confirm('Are you sure you want to cancel this order?')) return;
    setIsCancelling(true);
    try {
      if (!order.id.startsWith('demo-')) {
        const updated = await OrdersService.cancelOrder({
          id: order.id,
          requestBody: { reason: 'Customer requested cancellation from order tracking' },
        });
        if (onOrderUpdated) onOrderUpdated(updated);
      } else {
        if (onOrderUpdated) {
          onOrderUpdated({
            ...order,
            status: 'Cancelled',
            updatedAtUtc: new Date().toISOString(),
          });
        }
      }
    } finally {
      setIsCancelling(false);
    }
  };

  // Determine customer-friendly step (1..5)
  // 1: Placed, 2: Paid/Processing, 3: Confirmed & Preparing, 4: Shipped, 5: Delivered
  let currentStep = 1;
  if (orderState === 'Reserving' || orderState === 'Paying') currentStep = 2;
  if (orderState === 'Confirmed') currentStep = 3;
  if (shipmentState === 'HandedOver' || shipmentState === 'InTransit') currentStep = 4;
  if (shipmentState === 'Delivered') currentStep = 5;

  const isCancelled = orderState === 'Cancelled' || orderState === 'Refunding';

  const steps = [
    { label: 'Order Placed', icon: Clock },
    { label: 'Payment Confirmed', icon: CheckCircle2 },
    { label: 'Preparing', icon: Package },
    { label: 'Shipped', icon: Truck },
    { label: 'Delivered', icon: CheckCircle2 },
  ];

  const formatPaymentMethod = (method?: string | null) => {
    if (!method) return 'Credit Card';
    if (method.toLowerCase().includes('momo')) return 'MoMo E-Wallet';
    if (method.toLowerCase().includes('vnpay')) return 'VNPay Banking / QR';
    if (method.toLowerCase().includes('stripe') || method.toLowerCase().includes('credit'))
      return 'Credit / Debit Card';
    return method;
  };

  return (
    <div className="bg-white rounded-2xl shadow-xs border border-gray-100 p-6 sm:p-8 space-y-8">
      {/* Customer Order History List */}
      <div className="space-y-3 pb-6 border-b border-gray-100">
        <div className="flex items-center justify-between">
          <div>
            <h3 className="text-sm font-bold text-gray-900">My Orders ({allOrders.length})</h3>
            <p className="text-xs text-gray-500">
              Select an order below to view its receipt, items, and live delivery status.
            </p>
          </div>
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3 max-h-60 overflow-y-auto pr-1">
          {allOrders.map((itemOrder) => {
            const isSelected = itemOrder.id === order.id;
            const itemState = normalizeOrderState(itemOrder.status ?? undefined);
            const itemCount = (itemOrder.items ?? []).reduce(
              (sum, i) => sum + (i.quantity ?? 1),
              0
            );
            const createdLabel = itemOrder.createdAtUtc
              ? new Date(itemOrder.createdAtUtc).toLocaleString([], {
                  month: 'short',
                  day: 'numeric',
                  hour: '2-digit',
                  minute: '2-digit',
                })
              : 'Recent';

            return (
              <button
                key={itemOrder.id}
                type="button"
                onClick={() => {
                  setFetchedShipment(null);
                  if (onSelectOrder) {
                    onSelectOrder(itemOrder);
                  } else if (onOrderUpdated) {
                    onOrderUpdated(itemOrder);
                  }
                }}
                className={`text-left p-3.5 rounded-xl border transition-all cursor-pointer ${
                  isSelected
                    ? 'border-emerald-600 bg-emerald-50/50 ring-2 ring-emerald-500/20'
                    : 'border-gray-200 hover:border-gray-300 bg-gray-50/40 hover:bg-gray-50'
                }`}
              >
                <div className="flex items-center justify-between gap-2">
                  <span className="text-xs font-bold font-mono text-gray-900">
                    #{itemOrder.id?.slice(0, 8) ?? 'N/A'}
                  </span>
                  <Badge
                    variant={
                      itemState === 'Confirmed'
                        ? 'success'
                        : itemState === 'Cancelled' || itemState === 'Refunding'
                          ? 'danger'
                          : itemState === 'PaymentUnknown'
                            ? 'warning'
                            : 'info'
                    }
                  >
                    {itemState}
                  </Badge>
                </div>
                <div className="mt-2 flex items-center justify-between text-xs text-gray-600">
                  <span>
                    {itemCount} {itemCount === 1 ? 'item' : 'items'} &middot; {createdLabel}
                  </span>
                  <span className="font-bold text-gray-900">
                    {formatCurrencyAmount(itemOrder.totalAmount ?? 0, itemOrder.currency, false)}
                  </span>
                </div>
              </button>
            );
          })}
        </div>
      </div>

      {/* Top Banner */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-6 border-b border-gray-100">
        <div>
          <div className="flex items-center space-x-2">
            <h2 className="text-xl font-bold text-gray-900">
              {isCancelled
                ? 'Order Cancelled'
                : shipmentState === 'Delivered'
                  ? 'Order Delivered'
                  : shipmentState === 'HandedOver' || shipmentState === 'InTransit'
                    ? 'Order Shipped'
                    : 'Order Received'}
            </h2>
            <Badge
              variant={
                orderState === 'Confirmed'
                  ? 'success'
                  : isCancelled
                    ? 'danger'
                    : orderState === 'PaymentUnknown'
                      ? 'warning'
                      : 'info'
              }
            >
              {shipmentState === 'Delivered'
                ? 'Delivered'
                : shipmentState === 'HandedOver' || shipmentState === 'InTransit'
                  ? 'Shipped'
                  : orderState === 'Confirmed'
                    ? 'Order Confirmed'
                    : orderState === 'PaymentUnknown'
                      ? 'Verifying Payment'
                      : orderState === 'Reserving' || orderState === 'Paying'
                        ? 'Processing'
                        : orderState}
            </Badge>
          </div>
          <div className="flex items-center space-x-2 mt-1 text-xs text-gray-500">
            <span>Order #{order.id?.slice(0, 8) ?? 'N/A'}</span>
            <span>&middot;</span>
            <button
              type="button"
              onClick={handleCopyOrderId}
              className="inline-flex items-center text-gray-500 hover:text-gray-800 transition-colors"
              title="Copy full Order ID"
            >
              {copiedId ? (
                <>
                  <Check className="w-3.5 h-3.5 text-green-600 mr-1" />
                  Copied
                </>
              ) : (
                <>
                  <Copy className="w-3.5 h-3.5 mr-1" />
                  Copy ID
                </>
              )}
            </button>
          </div>
        </div>

        <div className="flex items-center space-x-3">
          <Button
            variant="outline"
            size="sm"
            isLoading={isRefreshing}
            onClick={() => {
              void syncOrderList();
              void syncOrderAndShipment(order.id);
            }}
          >
            <RefreshCw className="w-3.5 h-3.5 mr-1.5" />
            Refresh Status
          </Button>
          <Button variant="outline" size="sm" onClick={onContinueShopping}>
            Continue Shopping
            <ArrowRight className="w-4 h-4 ml-1.5" />
          </Button>
          {onViewStaffDetails && (
            <Button variant="secondary" size="sm" onClick={onViewStaffDetails}>
              ⚙️ Staff / Admin View
            </Button>
          )}
        </div>
      </div>

      {/* Reassuring Status Message */}
      {!isCancelled ? (
        <div className="bg-emerald-50/70 border border-emerald-200/80 rounded-2xl p-5 flex items-start space-x-3.5">
          <CheckCircle2 className="w-6 h-6 text-emerald-600 shrink-0 mt-0.5" />
          <div className="space-y-1">
            <h3 className="text-sm font-bold text-emerald-900">
              {shipmentState === 'Delivered'
                ? 'Your package has been delivered!'
                : shipmentState === 'HandedOver' || shipmentState === 'InTransit'
                  ? 'Your order is on the way!'
                  : orderState === 'Confirmed'
                    ? 'Thank you! Your order is confirmed.'
                    : 'Your order is being processed.'}
            </h3>
            <p className="text-xs text-emerald-800 leading-relaxed">
              {shipmentState === 'Delivered'
                ? 'Your shipment was delivered and signed for at your destination address.'
                : shipmentState === 'HandedOver' || shipmentState === 'InTransit'
                  ? 'Your package has been handed over to the carrier and is in transit to your delivery address.'
                  : orderState === 'Confirmed'
                    ? 'We have sent an order confirmation to your email. Your items will be packaged and dispatched promptly.'
                    : orderState === 'PaymentUnknown'
                      ? 'We are confirming the payment with your financial provider. Please do not re-submit; your order is safe.'
                      : 'We are completing the security checks and payment authorization for your order.'}
            </p>
          </div>
        </div>
      ) : (
        <div className="bg-red-50 border border-red-200 rounded-2xl p-5 flex items-start space-x-3.5">
          <AlertCircle className="w-6 h-6 text-red-600 shrink-0 mt-0.5" />
          <div>
            <h3 className="text-sm font-bold text-red-900">This order has been cancelled</h3>
            <p className="text-xs text-red-700 mt-1">
              Any charged amount has been credited back to your original payment method.
            </p>
          </div>
        </div>
      )}

      {/* Customer Progress Bar */}
      {!isCancelled && (
        <div className="py-2">
          <div className="relative">
            {/* Connecting line */}
            <div className="absolute top-5 left-6 right-6 h-0.5 bg-gray-200 -z-0" />
            <div
              className="absolute top-5 left-6 h-0.5 bg-green-600 transition-all duration-500 -z-0"
              style={{
                width: `${Math.min(100, Math.max(0, ((currentStep - 1) / (steps.length - 1)) * 100))}%`,
              }}
            />

            {/* Steps */}
            <div className="relative flex justify-between z-10">
              {steps.map((s, idx) => {
                const stepNum = idx + 1;
                const isCompleted = stepNum < currentStep;
                const isCurrent = stepNum === currentStep;
                const IconComponent = s.icon;

                return (
                  <div
                    key={s.label}
                    className="flex flex-col items-center text-center max-w-[80px]"
                  >
                    <div
                      className={`w-10 h-10 rounded-full flex items-center justify-center transition-colors shadow-xs ${
                        isCompleted
                          ? 'bg-green-600 text-white'
                          : isCurrent
                            ? 'bg-green-600 text-white ring-4 ring-green-100'
                            : 'bg-white border-2 border-gray-300 text-gray-400'
                      }`}
                    >
                      <IconComponent className="w-5 h-5" />
                    </div>
                    <span
                      className={`text-xs mt-2 font-medium ${
                        isCurrent
                          ? 'text-green-700 font-bold'
                          : isCompleted
                            ? 'text-gray-900'
                            : 'text-gray-400'
                      }`}
                    >
                      {s.label}
                    </span>
                  </div>
                );
              })}
            </div>
          </div>
        </div>
      )}

      {/* Shipment & Tracking Information */}
      {(order.trackingNumber || resolvedShipment?.trackingNumber) && (
        <div className="bg-gray-50 border border-gray-200/80 rounded-2xl p-5 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div className="flex items-center space-x-3">
            <div className="w-10 h-10 rounded-xl bg-blue-50 text-blue-600 flex items-center justify-center">
              <Truck className="w-5 h-5" />
            </div>
            <div>
              <span className="text-xs font-semibold uppercase tracking-wider text-gray-500 block">
                Carrier & Tracking
              </span>
              <span className="text-sm font-bold text-gray-900">
                {resolvedShipment?.carrier ?? 'Express Delivery'} &middot;{' '}
                <span className="font-mono text-blue-600">
                  {order.trackingNumber ?? resolvedShipment?.trackingNumber}
                </span>
              </span>
            </div>
          </div>

          <div className="text-xs text-gray-500">
            {shipmentState === 'Delivered' ? (
              <span className="text-green-700 font-bold">✓ Delivered to Recipient</span>
            ) : shipmentState === 'HandedOver' || shipmentState === 'InTransit' ? (
              <span className="text-blue-700 font-bold">In Transit with Carrier</span>
            ) : (
              <>
                Estimated Delivery: <strong className="text-gray-900">2–4 business days</strong>
              </>
            )}
          </div>
        </div>
      )}

      {/* Order Items & Summary */}
      <div className="space-y-4">
        <h3 className="text-base font-bold text-gray-900">Items Ordered</h3>
        <div className="divide-y divide-gray-100 border border-gray-200 rounded-xl overflow-hidden">
          {(order.items ?? []).map((item, idx) => (
            <div
              key={item.sku ?? idx}
              className="p-4 flex items-center justify-between hover:bg-gray-50 transition-colors"
            >
              <div className="flex items-center space-x-3">
                <div className="w-10 h-10 rounded-lg bg-gray-100 flex items-center justify-center text-gray-400 font-semibold text-xs">
                  📦
                </div>
                <div>
                  <h4 className="text-sm font-semibold text-gray-900">
                    {item.productName ?? item.sku}
                  </h4>
                  <span className="text-xs text-gray-500">Qty: {item.quantity ?? 1}</span>
                </div>
              </div>
              <div className="text-right">
                <span className="text-sm font-bold text-gray-900 block">
                  {formatCurrencyAmount(
                    item.lineTotal ?? (item.unitPrice ?? 0) * (item.quantity ?? 1),
                    item.currency ?? order.currency,
                    false
                  )}
                </span>
                <span className="text-xs text-gray-400">
                  {formatCurrencyAmount(
                    item.unitPrice ?? 0,
                    item.currency ?? order.currency,
                    false
                  )}{' '}
                  each
                </span>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Details Grid: Address, Payment, Total */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6 pt-4 border-t border-gray-100">
        <div>
          <span className="text-xs font-bold uppercase tracking-wider text-gray-400 block mb-2">
            Delivery Address
          </span>
          <div className="text-xs text-gray-700 space-y-0.5 leading-relaxed">
            <p className="font-semibold text-gray-900">
              {order.shippingAddress?.recipientName ?? 'Customer'}
            </p>
            <p>{order.shippingAddress?.street}</p>
            <p>
              {order.shippingAddress?.city}, {order.shippingAddress?.state}{' '}
              {order.shippingAddress?.postalCode}
            </p>
            <p className="font-medium text-gray-900">{order.shippingAddress?.country}</p>
            {order.shippingAddress?.phone ? (
              <p className="text-gray-500 pt-1">Tel: {order.shippingAddress.phone}</p>
            ) : null}
          </div>
        </div>

        <div>
          <span className="text-xs font-bold uppercase tracking-wider text-gray-400 block mb-2">
            Payment Details
          </span>
          <div className="text-xs text-gray-700 space-y-1">
            <p className="font-semibold text-gray-900">
              {formatPaymentMethod(order.paymentMethod)}
            </p>
            <p className="text-gray-500">
              Status:{' '}
              <span className="text-green-600 font-medium">
                {orderState === 'Confirmed' ? 'Paid & Verified' : orderState}
              </span>
            </p>
          </div>
        </div>

        <div className="bg-gray-50 rounded-xl p-4 flex flex-col justify-between">
          <div className="space-y-1.5 text-xs text-gray-600">
            <div className="flex justify-between">
              <span>Items Total:</span>
              <span>{formatCurrencyAmount(order.totalAmount ?? 0, order.currency, false)}</span>
            </div>
            <div className="flex justify-between text-green-600">
              <span>Shipping:</span>
              <span>FREE</span>
            </div>
            <div className="border-t border-gray-200 pt-2 flex justify-between text-sm font-bold text-gray-900">
              <span>Total Paid:</span>
              <span>{formatCurrencyAmount(order.totalAmount ?? 0, order.currency, true)}</span>
            </div>
          </div>

          {!isCancelled && orderState !== 'Confirmed' && (
            <div className="mt-4 pt-3 border-t border-gray-200">
              <Button
                variant="outline"
                size="sm"
                className="w-full text-red-600 hover:text-red-700"
                isLoading={isCancelling}
                onClick={handleCancelOrder}
              >
                Cancel Order
              </Button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
