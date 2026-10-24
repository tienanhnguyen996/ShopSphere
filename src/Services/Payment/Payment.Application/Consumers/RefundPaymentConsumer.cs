using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShopSphere.BuildingBlocks.Messaging.Contracts;
using ShopSphere.Services.Payment.Application.Common.Interfaces;
using ShopSphere.Services.Payment.Application.Contracts;
using ShopSphere.Services.Payment.Domain;

namespace ShopSphere.Services.Payment.Application.Consumers;

public class RefundPaymentConsumer :
    IConsumer<RefundPayment>,
    IConsumer<MessageEnvelope<RefundPayment>>
{
    private readonly IPaymentDbContext _dbContext;
    private readonly IPaymentProviderClient _providerClient;
    private readonly ILogger<RefundPaymentConsumer> _logger;

    public RefundPaymentConsumer(
        IPaymentDbContext dbContext,
        IPaymentProviderClient providerClient,
        ILogger<RefundPaymentConsumer> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _providerClient = providerClient ?? throw new ArgumentNullException(nameof(providerClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Consume(ConsumeContext<RefundPayment> context)
    {
        await HandleRefundAsync(context.Message, context, context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<MessageEnvelope<RefundPayment>> context)
    {
        await HandleRefundAsync(context.Message.Payload, context, context.CancellationToken);
    }

    public async Task HandleRefundAsync(
        RefundPayment command,
        IPublishEndpoint publishEndpoint,
        CancellationToken ct = default)
    {
        var orderId = command.OrderId;

        var attempt = command.PaymentAttemptId != Guid.Empty
            ? await _dbContext.PaymentAttempts.FirstOrDefaultAsync(p => p.Id == command.PaymentAttemptId, ct)
            : await _dbContext.PaymentAttempts.FirstOrDefaultAsync(p => p.OrderId == orderId, ct);

        attempt ??= await _dbContext.PaymentAttempts.FirstOrDefaultAsync(p => p.OrderId == orderId, ct);

        if (attempt is null)
        {
            _logger.LogWarning("No payment attempt found to refund for Order {OrderId}. Publishing synthetic refund completion.", orderId);
            await PublishRefundedAsync(
                orderId,
                command.PaymentAttemptId,
                $"rf_none_{orderId:N}",
                command.Amount,
                command.Currency,
                publishEndpoint,
                ct);
            return;
        }

        if (attempt.Status == PaymentStatus.Refunded)
        {
            _logger.LogInformation("PaymentAttempt {AttemptId} for Order {OrderId} is already refunded. Idempotent return.", attempt.Id, orderId);
            await PublishRefundedAsync(
                orderId,
                attempt.Id,
                attempt.ProviderTransactionId ?? $"rf_existing_{attempt.Id:N}",
                attempt.Amount,
                attempt.Currency,
                publishEndpoint,
                ct);
            return;
        }

        var refundResult = await _providerClient.RefundAsync(
            new PaymentRefundRequest(
                attempt.Id,
                attempt.ProviderTransactionId ?? string.Empty,
                command.Amount,
                command.Currency,
                command.Reason ?? "Order refund"),
            ct);

        if (refundResult.Status == PaymentProviderStatus.Success)
        {
            if (attempt.Status == PaymentStatus.Succeeded)
            {
                attempt.MarkRefunded(command.Reason ?? "Order refund");
                await _dbContext.SaveChangesAsync(ct);
            }

            await PublishRefundedAsync(
                orderId,
                attempt.Id,
                refundResult.RefundTransactionId ?? $"rf_{attempt.Id:N}",
                attempt.Amount,
                attempt.Currency,
                publishEndpoint,
                ct);
        }
        else
        {
            _logger.LogWarning("Refund call returned status {Status} for Order {OrderId}: {Error}",
                refundResult.Status, orderId, refundResult.ErrorMessage);
        }
    }

    private static async Task PublishRefundedAsync(
        Guid orderId,
        Guid attemptId,
        string refundTxId,
        decimal amount,
        string currency,
        IPublishEndpoint publishEndpoint,
        CancellationToken ct)
    {
        var evt = new PaymentRefunded(orderId, attemptId, refundTxId, amount, currency);
        var envelope = MessageEnvelope<PaymentRefunded>.Create(evt, orderId.ToString(), correlationId: orderId);

        await publishEndpoint.Publish(envelope, ct);
        await publishEndpoint.Publish(evt, ct);
    }
}
