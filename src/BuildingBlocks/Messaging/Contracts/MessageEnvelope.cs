namespace ShopSphere.BuildingBlocks.Messaging.Contracts;

/// <summary>
/// Mandatory message envelope contract conforming to ShopSphere messaging rules.
/// </summary>
public record MessageEnvelope<TPayload>
{
    public Guid MessageId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
    public Guid CausationId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
    public int ContractVersion { get; init; } = 1;
    public string BusinessId { get; init; } = string.Empty;
    public TPayload Payload { get; init; } = default!;

    public static MessageEnvelope<TPayload> Create(
        TPayload payload,
        string businessId,
        Guid? correlationId = null,
        Guid? causationId = null,
        int contractVersion = 1)
    {
        var messageId = Guid.NewGuid();
        return new MessageEnvelope<TPayload>
        {
            MessageId = messageId,
            CorrelationId = correlationId.HasValue && correlationId.Value != Guid.Empty ? correlationId.Value : Guid.NewGuid(),
            CausationId = causationId.HasValue && causationId.Value != Guid.Empty ? causationId.Value : messageId,
            OccurredAtUtc = DateTime.UtcNow,
            ContractVersion = contractVersion,
            BusinessId = businessId ?? string.Empty,
            Payload = payload
        };
    }
}
