using FluentAssertions;
using ShopSphere.BuildingBlocks.Messaging.Contracts;
using ShopSphere.BuildingBlocks.Messaging.Serialization;
using Xunit;

namespace ShopSphere.BuildingBlocks.Tests.Unit;

public enum OrderTestStatus
{
    PendingValidation,
    PaymentCompleted
}

public record TestOrderPayload(string OrderId, decimal Amount, OrderTestStatus Status);

public class MessageEnvelopeTests
{
    [Fact]
    public void Create_WithoutCorrelationId_GeneratesNewGuid()
    {
        var payload = new TestOrderPayload("ORD-100", 99.99m, OrderTestStatus.PendingValidation);
        var envelope = MessageEnvelope<TestOrderPayload>.Create(payload, "ORD-100");

        envelope.CorrelationId.Should().NotBeEmpty();
        envelope.MessageId.Should().NotBeEmpty();
        envelope.CausationId.Should().Be(envelope.MessageId);
        envelope.BusinessId.Should().Be("ORD-100");
    }

    [Fact]
    public void Create_WithCorrelationId_PreservesSuppliedId()
    {
        var correlationId = Guid.NewGuid();
        var causationId = Guid.NewGuid();
        var payload = new TestOrderPayload("ORD-101", 50.00m, OrderTestStatus.PaymentCompleted);

        var envelope = MessageEnvelope<TestOrderPayload>.Create(
            payload,
            "ORD-101",
            correlationId: correlationId,
            causationId: causationId,
            contractVersion: 2);

        envelope.CorrelationId.Should().Be(correlationId);
        envelope.CausationId.Should().Be(causationId);
        envelope.ContractVersion.Should().Be(2);
    }

    [Fact]
    public void Serialization_PreservesUtcDateTimeKindAndRoundTrips()
    {
        var originalDate = DateTime.UtcNow;
        var payload = new TestOrderPayload("ORD-200", 123.45m, OrderTestStatus.PendingValidation);
        var envelope = MessageEnvelope<TestOrderPayload>.Create(payload, "ORD-200") with
        {
            OccurredAtUtc = originalDate
        };

        var json = ShopSphereJsonDefaults.Serialize(envelope);

        json.Should().Contain("pendingValidation");
        json.Should().Contain("occurredAtUtc");

        var deserialized = ShopSphereJsonDefaults.Deserialize<MessageEnvelope<TestOrderPayload>>(json);

        deserialized.Should().NotBeNull();
        deserialized!.MessageId.Should().Be(envelope.MessageId);
        deserialized.CorrelationId.Should().Be(envelope.CorrelationId);
        deserialized.OccurredAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        deserialized.OccurredAtUtc.Should().BeCloseTo(originalDate, TimeSpan.FromMilliseconds(1));
        deserialized.Payload.Status.Should().Be(OrderTestStatus.PendingValidation);
        deserialized.Payload.Amount.Should().Be(123.45m);
    }
}
