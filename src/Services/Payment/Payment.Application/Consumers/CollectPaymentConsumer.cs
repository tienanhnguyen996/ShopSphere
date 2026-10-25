using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShopSphere.BuildingBlocks.Messaging.Contracts;
using ShopSphere.Services.Payment.Application.Common.Interfaces;
using ShopSphere.Services.Payment.Application.Contracts;
using ShopSphere.Services.Payment.Domain;

namespace ShopSphere.Services.Payment.Application.Consumers;

public class CollectPaymentConsumer :
    IConsumer<CollectPayment>,
    IConsumer<MessageEnvelope<CollectPayment>>
{
    private readonly IPaymentDbContext _dbContext;
    private readonly IPaymentProviderClient _providerClient;
    private readonly ILogger<CollectPaymentConsumer> _logger;

    public CollectPaymentConsumer(
        IPaymentDbContext dbContext,
        IPaymentProviderClient providerClient,
        ILogger<CollectPaymentConsumer> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _providerClient = providerClient ?? throw new ArgumentNullException(nameof(providerClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Consume(ConsumeContext<CollectPayment> context)
    {
        await HandlePaymentAsync(context.Message, context, context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<MessageEnvelope<CollectPayment>> context)
    {
        await HandlePaymentAsync(context.Message.Payload, context, context.CancellationToken);
    }

    public async Task HandlePaymentAsync(
        CollectPayment command,
        IPublishEndpoint publishEndpoint,
        CancellationToken ct = default)
    {
        var orderId = command.OrderId;

        // 1. Check for existing payment attempts for this order (idempotency check)
        var existingAttempt = await _dbContext.PaymentAttempts
            .FirstOrDefaultAsync(p => p.OrderId == orderId, ct);

        if (existingAttempt is not null)
        {
            if (existingAttempt.Status == PaymentStatus.Succeeded)
            {
                _logger.LogInformation("Duplicate CollectPayment received for Order {OrderId} which already succeeded.", orderId);
                var successEvent = new PaymentSucceeded(
                    orderId,
                    existingAttempt.Id,
                    existingAttempt.ProviderTransactionId ?? string.Empty,
                    existingAttempt.Amount,
                    existingAttempt.Currency);

                var envelope = MessageEnvelope<PaymentSucceeded>.Create(successEvent, orderId.ToString());
                await publishEndpoint.Publish(envelope, ct);
                await publishEndpoint.Publish(envelope.Payload, ct);
                return;
            }

            if (existingAttempt.Status == PaymentStatus.Failed)
            {
                _logger.LogInformation("Duplicate CollectPayment received for Order {OrderId} which already failed.", orderId);
                var rejectedEvent = new PaymentRejected(
                    orderId,
                    existingAttempt.Id,
                    existingAttempt.FailureReason ?? "Payment previously rejected.");

                var envelope = MessageEnvelope<PaymentRejected>.Create(rejectedEvent, orderId.ToString());
                await publishEndpoint.Publish(envelope, ct);
                await publishEndpoint.Publish(envelope.Payload, ct);
                return;
            }

            if (existingAttempt.Status == PaymentStatus.PaymentUnknown)
            {
                _logger.LogWarning("CollectPayment received for Order {OrderId} with status PaymentUnknown. Awaiting reconciliation, not retrying charge.", orderId);
                return;
            }

            if (existingAttempt.Status == PaymentStatus.Pending)
            {
                _logger.LogWarning("CollectPayment received for Order {OrderId} with an attempt already Pending. Awaiting completion, not retrying charge.", orderId);
                return;
            }
        }

        // 2. Create new PaymentAttempt in Pending state or use existing pending attempt
        var attempt = existingAttempt ?? PaymentAttempt.Create(
            command.OrderId,
            command.Amount,
            command.Currency,
            command.PaymentMethod);

        if (existingAttempt is null)
        {
            _dbContext.PaymentAttempts.Add(attempt);
            await _dbContext.SaveChangesAsync(ct);
        }

        // 3. Call external payment provider with timeout handling
        var chargeRequest = new PaymentChargeRequest(
            attempt.Id,
            command.OrderId,
            command.Amount,
            command.Currency,
            command.PaymentMethod);

        var chargeResult = await _providerClient.ChargeAsync(chargeRequest, ct);

        // 4. Update status and publish corresponding event
        const int maxRetries = 3;
        for (var retry = 1; retry <= maxRetries; retry++)
        {
            try
            {
                switch (chargeResult.Status)
                {
                    case PaymentProviderStatus.Success:
                        attempt.MarkSucceeded(chargeResult.ProviderTransactionId!);
                        _logger.LogInformation("Payment succeeded for Order {OrderId}, Attempt {AttemptId}, Tx {TxId}",
                            orderId, attempt.Id, attempt.ProviderTransactionId);

                        var successEvent = new PaymentSucceeded(
                            orderId,
                            attempt.Id,
                            attempt.ProviderTransactionId!,
                            attempt.Amount,
                            attempt.Currency);

                        var successEnvelope = MessageEnvelope<PaymentSucceeded>.Create(successEvent, orderId.ToString());

                        await _dbContext.SaveChangesAsync(ct);

                        await publishEndpoint.Publish(successEnvelope, ct);
                        await publishEndpoint.Publish(successEnvelope.Payload, ct);
                        if (_dbContext is DbContext efSuccessDb && efSuccessDb.ChangeTracker.HasChanges())
                        {
                            await _dbContext.SaveChangesAsync(ct);
                        }
                        return;

                    case PaymentProviderStatus.Failed:
                        var failureReason = chargeResult.ErrorMessage ?? "Payment rejected by provider.";
                        attempt.MarkFailed(failureReason, chargeResult.ProviderTransactionId);
                        _logger.LogWarning("Payment rejected for Order {OrderId}, Attempt {AttemptId}. Reason: {Reason}",
                            orderId, attempt.Id, failureReason);

                        var rejectedEvent = new PaymentRejected(
                            orderId,
                            attempt.Id,
                            failureReason);

                        var rejectedEnvelope = MessageEnvelope<PaymentRejected>.Create(rejectedEvent, orderId.ToString());

                        await _dbContext.SaveChangesAsync(ct);

                        await publishEndpoint.Publish(rejectedEnvelope, ct);
                        await publishEndpoint.Publish(rejectedEnvelope.Payload, ct);
                        if (_dbContext is DbContext efFailedDb && efFailedDb.ChangeTracker.HasChanges())
                        {
                            await _dbContext.SaveChangesAsync(ct);
                        }
                        return;

                    case PaymentProviderStatus.Unknown:
                    default:
                        // TIMEOUT = UNKNOWN, NOT FAILURE!
                        var unknownReason = chargeResult.ErrorMessage ?? "Payment provider timed out or returned unknown status.";
                        attempt.MarkUnknown(unknownReason);
                        _logger.LogWarning("Payment for Order {OrderId}, Attempt {AttemptId} timed out. Set to PaymentUnknown for reconciliation.",
                            orderId, attempt.Id);

                        var resolutionEvent = new PaymentPendingResolution(
                            orderId,
                            attempt.Id,
                            unknownReason);

                        var resolutionEnvelope = MessageEnvelope<PaymentPendingResolution>.Create(resolutionEvent, orderId.ToString());

                        await _dbContext.SaveChangesAsync(ct);

                        await publishEndpoint.Publish(resolutionEnvelope, ct);
                        await publishEndpoint.Publish(resolutionEnvelope.Payload, ct);
                        if (_dbContext is DbContext efUnknownDb && efUnknownDb.ChangeTracker.HasChanges())
                        {
                            await _dbContext.SaveChangesAsync(ct);
                        }
                        return;
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict updating payment attempt for Order {OrderId} on retry {Retry}", orderId, retry);
                if (retry == maxRetries)
                {
                    _logger.LogError("Exhausted concurrency retries updating payment attempt for Order {OrderId}", orderId);
                    throw;
                }

                var freshAttempt = await _dbContext.PaymentAttempts.FindAsync(new object[] { attempt.Id }, ct);
                if (freshAttempt is not null)
                {
                    attempt = freshAttempt;
                }
            }
        }
    }
}
