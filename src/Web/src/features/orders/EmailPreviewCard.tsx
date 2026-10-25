import React, { useState } from 'react';
import { NotificationJobDto, NotificationsService } from '../../api';
import { Badge, Button } from '../../shared/components';

export interface EmailPreviewCardProps {
  notifications: NotificationJobDto[];
  onNotificationUpdated: (updated: NotificationJobDto) => void;
}

export const EmailPreviewCard: React.FC<EmailPreviewCardProps> = ({
  notifications,
  onNotificationUpdated,
}) => {
  const [retryingId, setRetryingId] = useState<string | null>(null);

  const handleRetry = async (job: NotificationJobDto) => {
    const jobId = job.id ?? '';
    if (!jobId) return;
    setRetryingId(jobId);
    try {
      if (!jobId.startsWith('demo-')) {
        const refreshed = await NotificationsService.retryNotification({ id: jobId });
        onNotificationUpdated(refreshed);
      } else {
        onNotificationUpdated({
          ...job,
          status: 'Sent',
          retryCount: (job.retryCount ?? 0) + 1,
          sentAtUtc: new Date().toISOString(),
          lastError: null,
        });
      }
    } finally {
      setRetryingId(null);
    }
  };

  return (
    <div
      style={{
        padding: '18px',
        borderRadius: '12px',
        border: '1px solid #e2e8f0',
        backgroundColor: '#ffffff',
        display: 'flex',
        flexDirection: 'column',
        gap: '12px',
      }}
    >
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h3 style={{ margin: 0, fontSize: '16px', fontWeight: 700, color: '#0f172a' }}>
          Order Confirmation Email & SMS Dispatches
        </h3>
        <Badge variant="info">{notifications.length} Dispatched</Badge>
      </div>

      {notifications.length === 0 ? (
        <div style={{ fontSize: '13px', color: '#64748b' }}>
          No customer notifications dispatched for this order yet.
        </div>
      ) : (
        <div style={{ display: 'grid', gap: '12px' }}>
          {notifications.map((job, idx) => {
            const isFailed = job.status === 'Failed';
            const isSent = job.status === 'Sent';
            return (
              <div
                key={job.id ?? idx}
                style={{
                  padding: '14px',
                  borderRadius: '10px',
                  backgroundColor: '#f8fafc',
                  border: '1px solid #cbd5e1',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: '8px',
                }}
              >
                <div
                  style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    flexWrap: 'wrap',
                    gap: '6px',
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                    <Badge variant={isSent ? 'success' : isFailed ? 'danger' : 'warning'}>
                      {job.channel ?? 'Email'}: {job.status ?? 'Pending'}
                    </Badge>
                    <strong style={{ fontSize: '13px', color: '#0f172a' }}>
                      {job.subject ?? 'ShopSphere Order Update'}
                    </strong>
                  </div>
                  <span style={{ fontSize: '11px', color: '#64748b' }}>
                    To: <code>{job.recipient ?? 'customer@shopsphere.local'}</code> • Retries:{' '}
                    {job.retryCount ?? 0}
                  </span>
                </div>

                <div
                  style={{
                    padding: '10px 12px',
                    borderRadius: '8px',
                    backgroundColor: '#ffffff',
                    border: '1px solid #e2e8f0',
                    fontSize: '12px',
                    color: '#334155',
                    fontFamily: 'monospace',
                    whiteSpace: 'pre-wrap',
                  }}
                >
                  {job.body ??
                    'Thank you for your ShopSphere order! Your payment and inventory reservation have been confirmed.'}
                </div>

                {job.lastError && (
                  <div style={{ fontSize: '12px', color: '#dc2626', fontWeight: 600 }}>
                    Last Provider Error: {job.lastError}
                  </div>
                )}

                {isFailed && (
                  <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
                    <Button
                      variant="secondary"
                      isLoading={retryingId === job.id}
                      onClick={() => handleRetry(job)}
                    >
                      Retry Dispatch
                    </Button>
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
};
