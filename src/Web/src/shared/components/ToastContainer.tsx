import React from 'react';
import { CheckCircle2, AlertTriangle, Info, XCircle, X } from 'lucide-react';
import { ToastMessage } from '../types';

export interface ToastContainerProps {
  toasts: ToastMessage[];
  onDismiss: (id: string) => void;
}

export const ToastContainer: React.FC<ToastContainerProps> = ({ toasts, onDismiss }) => {
  if (toasts.length === 0) {
    return null;
  }

  const renderIcon = (variant: ToastMessage['variant']): React.ReactNode => {
    switch (variant) {
      case 'success':
        return <CheckCircle2 className="w-5 h-5 text-green-600 shrink-0" />;
      case 'warning':
        return <AlertTriangle className="w-5 h-5 text-amber-600 shrink-0" />;
      case 'danger':
        return <XCircle className="w-5 h-5 text-red-600 shrink-0" />;
      case 'info':
        return <Info className="w-5 h-5 text-blue-600 shrink-0" />;
      default: {
        const _exhaustive: never = variant;
        return _exhaustive;
      }
    }
  };

  return (
    <div
      className="fixed bottom-4 right-4 z-50 flex flex-col space-y-2 max-w-sm w-full pointer-events-none"
      aria-live="polite"
    >
      {toasts.map((toast) => (
        <div
          key={toast.id}
          className="pointer-events-auto bg-white rounded-xl shadow-lg border border-gray-200 p-4 flex items-start space-x-3"
        >
          {renderIcon(toast.variant)}
          <div className="flex-1 min-w-0">
            <p className="text-sm font-semibold text-gray-900">{toast.title}</p>
            <p className="text-xs text-gray-600 mt-0.5">{toast.description}</p>
          </div>
          <button
            type="button"
            onClick={() => onDismiss(toast.id)}
            className="text-gray-400 hover:text-gray-600 p-0.5 rounded"
            aria-label="Dismiss notification"
          >
            <X className="w-4 h-4" />
          </button>
        </div>
      ))}
    </div>
  );
};
