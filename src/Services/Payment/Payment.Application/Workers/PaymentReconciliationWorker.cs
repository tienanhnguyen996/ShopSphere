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
        _interval = interval ?? TimeSpan.FromSeconds(8);
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
            const int maxRetries = 3;
            for (var retry = 0; retry < maxRetries; retry++)
            {
                try
                {
                    var currentAttempt = attempt;
                    if (retry > 0)
                    {
                        var fresh = await dbContext.PaymentAttempts.FirstOrDefaultAsync(p => p.Id == attempt.Id, ct);
                        if (fresh is null)
                        {
                            _logger.LogInformation("PaymentAttempt {AttemptId} not found on reload. Skipping.", attempt.Id);
                            break;
                        }
                        currentAttempt = fresh;
                    }

                    var queryRequest = new PaymentStatusQueryRequest(currentAttempt.Id, currentAttempt.ProviderTransactionId);
                    var queryResult = await providerClient.GetStatusAsync(queryRequest, ct);

                    if (queryResult.Status == PaymentProviderStatus.Success)
                    {
                        var transactionId = queryResult.ProviderTransactionId ?? currentAttempt.ProviderTransactionId ?? $"reconciled_tx_{currentAttempt.Id:N}";
                        if (currentAttempt.Status == PaymentStatus.PaymentUnknown)
                        {
                            currentAttempt.MarkSucceeded(transactionId);
                            _logger.LogInformation("Reconciled PaymentAttempt {AttemptId} for Order {OrderId} as Succeeded. Tx: {TxId}",
                                currentAttempt.Id, currentAttempt.OrderId, transactionId);
                        }
                        else if (currentAttempt.Status != PaymentStatus.Succeeded)
                        {
                            _logger.LogWarning("PaymentAttempt {AttemptId} already resolved to conflicting status {Status}. Skipping.",
                                currentAttempt.Id, currentAttempt.Status);
                            break;
                        }

                        var successEvent = new PaymentSucceeded(
                            currentAttempt.OrderId,
                            currentAttempt.Id,
                            transactionId,
                            currentAttempt.Amount,
                            currentAttempt.Currency);

                        var envelope = MessageEnvelope<PaymentSucceeded>.Create(successEvent, currentAttempt.OrderId.ToString());

                        await dbContext.SaveChangesAsync(ct);
                        resolvedCount++;

                        await publishEndpoint.Publish(envelope, ct);
                        await publishEndpoint.Publish(envelope.Payload, ct);
                        if (dbContext is DbContext efSuccessDb && efSuccessDb.ChangeTracker.HasChanges())
                        {
                            await dbContext.SaveChangesAsync(ct);
                        }
                        break;
                    }
                    else if (queryResult.Status == PaymentProviderStatus.Failed)
                    {
                        var reason = queryResult.ErrorMessage ?? "Payment confirmed failed during reconciliation.";
                        if (currentAttempt.Status == PaymentStatus.PaymentUnknown)
                        {
                            currentAttempt.MarkFailed(reason, queryResult.ProviderTransactionId);
                            _logger.LogInformation("Reconciled PaymentAttempt {AttemptId} for Order {OrderId} as Failed. Reason: {Reason}",
                                currentAttempt.Id, currentAttempt.OrderId, reason);
                        }
                        else if (currentAttempt.Status != PaymentStatus.Failed)
                        {
                            _logger.LogWarning("PaymentAttempt {AttemptId} already resolved to conflicting status {Status}. Skipping.",
                                currentAttempt.Id, currentAttempt.Status);
                            break;
                        }

                        var rejectedEvent = new PaymentRejected(
                            currentAttempt.OrderId,
                            currentAttempt.Id,
                            reason);

                        var envelope = MessageEnvelope<PaymentRejected>.Create(rejectedEvent, currentAttempt.OrderId.ToString());

                        await dbContext.SaveChangesAsync(ct);
                        resolvedCount++;

                        await publishEndpoint.Publish(envelope, ct);
                        await publishEndpoint.Publish(envelope.Payload, ct);
                        if (dbContext is DbContext efFailedDb && efFailedDb.ChangeTracker.HasChanges())
                        {
                            await dbContext.SaveChangesAsync(ct);
                        }
                        break;
                    }
                    else
                    {
                        _logger.LogInformation("PaymentAttempt {AttemptId} for Order {OrderId} is still Unknown at provider. Will re-check on next cycle.",
                            currentAttempt.Id, currentAttempt.OrderId);
                        break;
                    }
                }
                catch (DbUpdateConcurrencyException ex) when (retry < maxRetries - 1)
                {
                    _logger.LogWarning(ex, "Concurrency conflict reconciling PaymentAttempt {AttemptId}, retry attempt {Retry}",
                        attempt.Id, retry + 1);
                    await Task.Delay(50 * (retry + 1), ct);
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogWarning(ex, "Concurrency conflict reconciling PaymentAttempt {AttemptId}. Retries exhausted; state was modified concurrently.", attempt.Id);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reconciling PaymentAttempt {AttemptId}", attempt.Id);
                    break;
                }
            }
        }

        return resolvedCount;
    }
}
