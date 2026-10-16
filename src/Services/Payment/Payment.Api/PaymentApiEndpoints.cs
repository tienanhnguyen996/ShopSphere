using System.Text.Json;
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ShopSphere.BuildingBlocks.Messaging.Contracts;
using ShopSphere.BuildingBlocks.Security;
using ShopSphere.Services.Payment.Api.Services;
using ShopSphere.Services.Payment.Application.Common.Interfaces;
using ShopSphere.Services.Payment.Application.Contracts;
using ShopSphere.Services.Payment.Domain;

namespace ShopSphere.Services.Payment.Api;

public record ProviderWebhookPayload(
    string EventId,
    string EventType,
    Guid PaymentAttemptId,
    string? ProviderTransactionId,
    string Status,
    string? Reason);

public record CreatePaymentAttemptRequest(
    Guid OrderId,
    decimal Amount,
    string Currency,
    string PaymentMethod);

public record PaymentAttemptResponse(
    Guid Id,
    Guid OrderId,
    decimal Amount,
    string Currency,
    string PaymentMethod,
    string Status,
    string? ProviderTransactionId,
    string? FailureReason,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public static class PaymentApiEndpoints
{
    public static WebApplication MapPaymentEndpoints(this WebApplication app)
    {
        // Health check endpoint
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                var response = new
                {
                    status = report.Status.ToString(),
                    totalDuration = report.TotalDuration.ToString(),
                    entries = report.Entries.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new
                        {
                            status = kvp.Value.Status.ToString(),
                            description = kvp.Value.Description,
                            duration = kvp.Value.Duration.ToString(),
                            error = kvp.Value.Exception?.Message
                        })
                };

                var statusCode = report.Status == HealthStatus.Healthy
                    ? StatusCodes.Status200OK
                    : StatusCodes.Status503ServiceUnavailable;

                context.Response.StatusCode = statusCode;
                await context.Response.WriteAsJsonAsync(response);
            }
        })
        .WithName("HealthCheck")
        .WithTags("Health")
        .WithOpenApi();

        var group = app.MapGroup("/api/v1/payment")
            .WithTags("Payment");

        // POST /api/v1/payment/webhooks/mock-provider
        group.MapPost("/webhooks/mock-provider", async (
            HttpRequest request,
            [FromHeader(Name = "X-Payment-Signature")] string? signatureHeader,
            IPaymentWebhookSignatureValidator validator,
            IConfiguration config,
            IPaymentDbContext dbContext,
            IPublishEndpoint publishEndpoint,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("PaymentWebhook");
            using var reader = new StreamReader(request.Body);
            var rawBody = await reader.ReadToEndAsync(ct);

            var secret = config["Payment:WebhookSecret"] ?? "ShopSpherePaymentWebhookSecret2026";

            if (string.IsNullOrWhiteSpace(signatureHeader) ||
                !validator.ValidateSignature(rawBody, signatureHeader, secret))
            {
                logger.LogWarning("Webhook signature verification failed or missing.");
                return Results.Json(new
                {
                    type = "Unauthorized",
                    message = "Invalid or missing X-Payment-Signature header."
                }, statusCode: StatusCodes.Status401Unauthorized);
            }

            ProviderWebhookPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<ProviderWebhookPayload>(rawBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to parse webhook JSON payload.");
                return Results.BadRequest(new { type = "Validation", message = "Invalid JSON body format." });
            }

            if (payload is null || string.IsNullOrWhiteSpace(payload.EventId) || payload.PaymentAttemptId == Guid.Empty)
            {
                return Results.BadRequest(new { type = "Validation", message = "EventId and PaymentAttemptId are required." });
            }

            // Idempotency: verify if webhook event was already recorded
            var alreadyProcessed = await dbContext.WebhookLogs
                .AnyAsync(w => w.EventId == payload.EventId, ct);

            if (alreadyProcessed)
            {
                logger.LogInformation("Webhook event {EventId} was already processed. Acknowledging idempotently.", payload.EventId);
                return Results.Ok(new { status = "already_processed", eventId = payload.EventId });
            }

            var webhookLog = PaymentWebhookLog.Create(payload.EventId, payload.EventType, rawBody);
            dbContext.WebhookLogs.Add(webhookLog);

            // Locate target payment attempt
            var attempt = await dbContext.PaymentAttempts
                .FirstOrDefaultAsync(p => p.Id == payload.PaymentAttemptId, ct);

            if (attempt is not null)
            {
                var isSucceeded = payload.Status.Equals("succeeded", StringComparison.OrdinalIgnoreCase);
                var isFailed = payload.Status.Equals("failed", StringComparison.OrdinalIgnoreCase);

                if (attempt.Status is PaymentStatus.Pending or PaymentStatus.PaymentUnknown)
                {
                    if (isSucceeded)
                    {
                        var txId = payload.ProviderTransactionId ?? $"tx_hook_{payload.EventId}";
                        attempt.MarkSucceeded(txId);

                        var successEvent = new PaymentSucceeded(
                            attempt.OrderId,
                            attempt.Id,
                            txId,
                            attempt.Amount,
                            attempt.Currency);

                        var envelope = MessageEnvelope<PaymentSucceeded>.Create(successEvent, attempt.OrderId.ToString());
                        await publishEndpoint.Publish(envelope, ct);
                        await publishEndpoint.Publish(envelope.Payload, ct);
                        logger.LogInformation("Webhook transitioned PaymentAttempt {AttemptId} to Succeeded.", attempt.Id);
                    }
                    else if (isFailed)
                    {
                        var reason = payload.Reason ?? "Payment declined by provider webhook.";
                        attempt.MarkFailed(reason, payload.ProviderTransactionId);

                        var rejectedEvent = new PaymentRejected(
                            attempt.OrderId,
                            attempt.Id,
                            reason);

                        var envelope = MessageEnvelope<PaymentRejected>.Create(rejectedEvent, attempt.OrderId.ToString());
                        await publishEndpoint.Publish(envelope, ct);
                        await publishEndpoint.Publish(envelope.Payload, ct);
                        logger.LogInformation("Webhook transitioned PaymentAttempt {AttemptId} to Failed.", attempt.Id);
                    }
                }
                else
                {
                    logger.LogInformation(
                        "Webhook event {EventId} arrived for attempt {AttemptId} in final status {Status}. Late event ignored.",
                        payload.EventId, attempt.Id, attempt.Status);
                }
            }
            else
            {
                logger.LogWarning("Webhook event {EventId} references unknown PaymentAttemptId {AttemptId}.",
                    payload.EventId, payload.PaymentAttemptId);
            }

            await dbContext.SaveChangesAsync(ct);

            return Results.Ok(new
            {
                status = "processed",
                eventId = payload.EventId,
                paymentAttemptId = payload.PaymentAttemptId
            });
        })
        .WithName("ProviderWebhook")
        .WithSummary("Receives provider webhook callbacks with HMAC-SHA256 signature verification")
        .Accepts<ProviderWebhookPayload>("application/json")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .WithOpenApi();

        // GET /api/v1/payment/attempts/{id}
        group.MapGet("/attempts/{id:guid}", async (
            Guid id,
            IPaymentDbContext dbContext,
            CancellationToken ct) =>
        {
            var attempt = await dbContext.PaymentAttempts
                .FirstOrDefaultAsync(p => p.Id == id, ct);

            if (attempt is null)
            {
                return Results.NotFound(new { type = "NotFound", message = $"Payment attempt {id} not found." });
            }

            var response = new PaymentAttemptResponse(
                attempt.Id,
                attempt.OrderId,
                attempt.Amount,
                attempt.Currency,
                attempt.PaymentMethod,
                attempt.Status.ToString(),
                attempt.ProviderTransactionId,
                attempt.FailureReason,
                attempt.CreatedAtUtc,
                attempt.UpdatedAtUtc);

            return Results.Ok(response);
        })
        .WithName("GetPaymentAttemptById")
        .WithSummary("Retrieve details of a payment attempt by identifier")
        .Produces<PaymentAttemptResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .RequirePermission(ShopSpherePermissions.Payment.Read)
        .WithOpenApi();

        // POST /api/v1/payment/attempts
        group.MapPost("/attempts", async (
            [FromBody] CreatePaymentAttemptRequest request,
            IPaymentDbContext dbContext,
            CancellationToken ct) =>
        {
            var attempt = PaymentAttempt.Create(request.OrderId, request.Amount, request.Currency, request.PaymentMethod);
            dbContext.PaymentAttempts.Add(attempt);
            await dbContext.SaveChangesAsync(ct);

            var response = new PaymentAttemptResponse(
                attempt.Id,
                attempt.OrderId,
                attempt.Amount,
                attempt.Currency,
                attempt.PaymentMethod,
                attempt.Status.ToString(),
                attempt.ProviderTransactionId,
                attempt.FailureReason,
                attempt.CreatedAtUtc,
                attempt.UpdatedAtUtc);

            return Results.Created($"/api/v1/payment/attempts/{attempt.Id}", response);
        })
        .WithName("CreatePaymentAttempt")
        .WithSummary("Create a new payment attempt for an order")
        .Produces<PaymentAttemptResponse>(StatusCodes.Status201Created)
        .RequirePermission(ShopSpherePermissions.Payment.Create)
        .WithOpenApi();

        // GET /api/v1/payment/orders/{orderId}
        group.MapGet("/orders/{orderId:guid}", async (
            Guid orderId,
            IPaymentDbContext dbContext,
            CancellationToken ct) =>
        {
            var attempt = await dbContext.PaymentAttempts
                .OrderByDescending(p => p.CreatedAtUtc)
                .FirstOrDefaultAsync(p => p.OrderId == orderId, ct);

            if (attempt is null)
            {
                return Results.NotFound(new { type = "NotFound", message = $"No payment attempt found for order {orderId}." });
            }

            var response = new PaymentAttemptResponse(
                attempt.Id,
                attempt.OrderId,
                attempt.Amount,
                attempt.Currency,
                attempt.PaymentMethod,
                attempt.Status.ToString(),
                attempt.ProviderTransactionId,
                attempt.FailureReason,
                attempt.CreatedAtUtc,
                attempt.UpdatedAtUtc);

            return Results.Ok(response);
        })
        .WithName("GetPaymentAttemptByOrderId")
        .WithSummary("Retrieve the latest payment attempt for an order")
        .Produces<PaymentAttemptResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .WithOpenApi();

        return app;
    }
}
