import React from 'react';
import { OrderState } from '../types';
import { Badge } from './Badge';

export interface OrderStatusBadgeProps {
  status: OrderState;
  className?: string;
}

export const OrderStatusBadge: React.FC<OrderStatusBadgeProps> = ({ status, className = '' }) => {
  const getBadgeConfig = (
    state: OrderState
  ): { text: string; variant: 'default' | 'success' | 'warning' | 'danger' | 'info' } => {
    switch (state) {
      case 'Pending':
        return { text: 'Order Pending', variant: 'default' };
      case 'Reserving':
        return { text: 'Reserving Inventory', variant: 'info' };
      case 'Paying':
        return { text: 'Processing Payment', variant: 'info' };
      case 'PaymentUnknown':
        return { text: 'Payment Status Unknown', variant: 'warning' };
      case 'Confirmed':
        return { text: 'Confirmed', variant: 'success' };
      case 'Refunding':
        return { text: 'Refunding', variant: 'warning' };
      case 'Cancelled':
        return { text: 'Cancelled', variant: 'danger' };
      default: {
        const _exhaustiveCheck: never = state;
        return { text: _exhaustiveCheck, variant: 'default' };
      }
    }
  };

  const config = getBadgeConfig(status);

  return (
    <Badge variant={config.variant} className={className}>
      {config.text}
    </Badge>
  );
};
