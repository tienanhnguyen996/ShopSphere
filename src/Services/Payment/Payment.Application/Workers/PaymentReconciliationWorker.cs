using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShopSphere.BuildingBlocks.Messaging.Contracts;
using ShopSphere.Services.Payment.Application.Common.Interfaces;
using ShopSphere.Services.Payment.Application.Contracts;
using ShopSphere.Services.Payment.Domain;

namespace ShopSphere.Services.Payment.Application.Workers;

/// <summary>
/// Background worker that queries external payment provider for unresolved PaymentUnknown attempts,
/// applies reconciliation state transitions, and emits outbox events.
/// </summary>
public class PaymentReconciliationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PaymentReconciliationWorker> _logger;
    private readonly TimeSpan _interval;
    private readonly int _batchSize;

    public PaymentReconciliationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<PaymentReconciliationWorker> logger,
        TimeSpan? interval = null,
        int batchSize = 50)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _interval = interval ?? TimeSpan.FromSeconds(30);
        _batchSize = batchSize > 0 ? batchSize : 50;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PaymentReconciliationWorker started with interval {Interval}", _interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcileUnknownPaymentsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred during payment reconciliation sweep");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("PaymentReconciliationWorker stopped.");
    }

    /// <summary>
    /// Executes a single reconciliation cycle for unresolved PaymentUnknown attempts.
    /// Can be called directly by unit and integration tests.
    /// </summary>
    public async Task<int> ReconcileUnknownPaymentsAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IPaymentDbContext>();
        var providerClient = scope.ServiceProvider.GetRequiredService<IPaymentProviderClient>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var unknownAttempts = await dbContext.PaymentAttempts
            .Where(p => p.Status == PaymentStatus.PaymentUnknown)
            .OrderBy(p => p.CreatedAtUtc)
            .Take(_batchSize)
            .ToListAsync(ct);

        if (unknownAttempts.Count == 0)
        {
            return 0;
        }

        var resolvedCount = 0;

        foreach (var attempt in unknownAttempts)
        {
            try
            {
                var queryRequest = new PaymentStatusQueryRequest(attempt.Id, attempt.ProviderTransactionId);
                var queryResult = await providerClient.GetStatusAsync(queryRequest, ct);

                if (queryResult.Status == PaymentProviderStatus.Success)
                {
                    var transactionId = queryResult.ProviderTransactionId ?? attempt.ProviderTransactionId ?? $"reconciled_tx_{attempt.Id:N}";
                    attempt.MarkSucceeded(transactionId);
                    _logger.LogInformation("Reconciled PaymentAttempt {AttemptId} for Order {OrderId} as Succeeded. Tx: {TxId}",
                        attempt.Id, attempt.OrderId, transactionId);

                    var successEvent = new PaymentSucceeded(
                        attempt.OrderId,
                        attempt.Id,
                        transactionId,
                        attempt.Amount,
                        attempt.Currency);

                    var envelope = MessageEnvelope<PaymentSucceeded>.Create(successEvent, attempt.OrderId.ToString());
                    await publishEndpoint.Publish(envelope, ct);
                    await publishEndpoint.Publish(envelope.Payload, ct);

                    await dbContext.SaveChangesAsync(ct);
                    resolvedCount++;
                }
                else if (queryResult.Status == PaymentProviderStatus.Failed)
                {
                    var reason = queryResult.ErrorMessage ?? "Payment confirmed failed during reconciliation.";
                    attempt.MarkFailed(reason, queryResult.ProviderTransactionId);
                    _logger.LogInformation("Reconciled PaymentAttempt {AttemptId} for Order {OrderId} as Failed. Reason: {Reason}",
                        attempt.Id, attempt.OrderId, reason);

                    var rejectedEvent = new PaymentRejected(
                        attempt.OrderId,
                        attempt.Id,
                        reason);

                    var envelope = MessageEnvelope<PaymentRejected>.Create(rejectedEvent, attempt.OrderId.ToString());
                    await publishEndpoint.Publish(envelope, ct);
                    await publishEndpoint.Publish(envelope.Payload, ct);

                    await dbContext.SaveChangesAsync(ct);
                    resolvedCount++;
                }
                else
                {
                    _logger.LogInformation("PaymentAttempt {AttemptId} for Order {OrderId} is still Unknown at provider. Will re-check on next cycle.",
                        attempt.Id, attempt.OrderId);
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict reconciling PaymentAttempt {AttemptId}. State was modified concurrently.", attempt.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reconciling PaymentAttempt {AttemptId}", attempt.Id);
            }
        }

        return resolvedCount;
    }
}
