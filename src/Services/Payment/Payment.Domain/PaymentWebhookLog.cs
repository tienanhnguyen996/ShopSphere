namespace ShopSphere.Services.Payment.Domain;

/// <summary>
/// Entity tracking received provider webhooks for idempotency and audit trail.
/// </summary>
public class PaymentWebhookLog
{
    public Guid Id { get; private set; }
    public string EventId { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTime ProcessedAtUtc { get; private set; }

    // Parameterless constructor for EF Core
    private PaymentWebhookLog() { }

    public PaymentWebhookLog(Guid id, string eventId, string eventType, string payload)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            throw new ArgumentException("EventId cannot be empty.", nameof(eventId));
        }

        Id = id;
        EventId = eventId.Trim();
        EventType = eventType?.Trim() ?? string.Empty;
        Payload = payload ?? string.Empty;
        ProcessedAtUtc = DateTime.UtcNow;
    }

    public static PaymentWebhookLog Create(string eventId, string eventType, string payload)
    {
        return new PaymentWebhookLog(Guid.NewGuid(), eventId, eventType, payload);
    }
}
