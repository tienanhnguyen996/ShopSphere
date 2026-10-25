import React from 'react';
import { Badge } from '../../shared/components';
import { StockLevelStatus } from '../../shared/types';

export interface StockAvailabilityBadgeProps {
  availableQuantity: number | undefined;
  isLoading?: boolean;
}

function resolveStockLevelStatus(
  availableQuantity: number | undefined,
  isLoading: boolean
): StockLevelStatus {
  if (isLoading || availableQuantity === undefined) {
    return 'loading';
  }
  if (availableQuantity <= 0) {
    return 'out_of_stock';
  }
  if (availableQuantity <= 5) {
    return 'low_stock';
  }
  return 'in_stock';
}

export const StockAvailabilityBadge: React.FC<StockAvailabilityBadgeProps> = ({
  availableQuantity,
  isLoading = false,
}) => {
  const status = resolveStockLevelStatus(availableQuantity, isLoading);

  switch (status) {
    case 'loading':
      return <Badge variant="default">Checking Stock...</Badge>;
    case 'out_of_stock':
      return <Badge variant="danger">Out of Stock (0 left)</Badge>;
    case 'low_stock':
      return <Badge variant="warning">Low Stock ({availableQuantity ?? 0} left)</Badge>;
    case 'in_stock':
      return <Badge variant="success">In Stock ({availableQuantity ?? 0} available)</Badge>;
    default: {
      const _exhaustive: never = status;
      return _exhaustive;
    }
  }
};
