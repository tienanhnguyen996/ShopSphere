import React, { useEffect, useState } from 'react';
import { Wifi, WifiOff } from 'lucide-react';

export const OfflineToast: React.FC = () => {
  const [isOnline, setIsOnline] = useState<boolean>(() =>
    typeof navigator !== 'undefined' ? navigator.onLine : true
  );
  const [showReconnected, setShowReconnected] = useState<boolean>(false);

  useEffect(() => {
    const handleOffline = (): void => {
      setIsOnline(false);
      setShowReconnected(false);
    };

    const handleOnline = (): void => {
      setIsOnline(true);
      setShowReconnected(true);
    };

    window.addEventListener('offline', handleOffline);
    window.addEventListener('online', handleOnline);

    return () => {
      window.removeEventListener('offline', handleOffline);
      window.removeEventListener('online', handleOnline);
    };
  }, []);

  useEffect(() => {
    if (!showReconnected) {
      return;
    }

    const timer = window.setTimeout(() => {
      setShowReconnected(false);
    }, 3500);

    return () => {
      window.clearTimeout(timer);
    };
  }, [showReconnected]);

  if (isOnline && !showReconnected) {
    return null;
  }

  return (
    <div
      className="fixed bottom-4 left-4 z-50 flex items-center space-x-3 rounded-lg px-4 py-3 shadow-lg border text-sm font-medium transition-all"
      role="status"
      aria-live="polite"
      style={{
        backgroundColor: isOnline ? '#ecfdf5' : '#fef2f2',
        borderColor: isOnline ? '#6ee7b7' : '#fca5a5',
        color: isOnline ? '#065f46' : '#991b1b',
      }}
    >
      {isOnline ? (
        <>
          <Wifi className="w-4 h-4 text-green-600 shrink-0" />
          <span>Connection restored — syncing live order &amp; cart state with Gateway.</span>
        </>
      ) : (
        <>
          <WifiOff className="w-4 h-4 text-red-600 shrink-0" />
          <span>You are offline — checkout actions are paused until connection resumes.</span>
        </>
      )}
    </div>
  );
};
