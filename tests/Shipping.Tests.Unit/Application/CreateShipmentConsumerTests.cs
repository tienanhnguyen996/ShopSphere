using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShopSphere.BuildingBlocks.Messaging.Contracts;
using ShopSphere.Services.Shipping.Application.Abstractions;
using ShopSphere.Services.Shipping.Application.Consumers;
using ShopSphere.Services.Shipping.Application.Contracts;
using ShopSphere.Services.Shipping.Domain;
using ShopSphere.Services.Shipping.Infrastructure.Carriers;
using ShopSphere.Services.Shipping.Tests.Unit.Helpers;

namespace ShopSphere.Services.Shipping.Tests.Unit.Application;

public class CreateShipmentConsumerTests
{
    private static (Mock<IShippingDbContext> DbMock, List<Shipment> Store) CreateMockDbContext()
    {
        var store = new List<Shipment>();
        var mockSet = MockDbSetExtensions.CreateMockDbSet(store);
        var dbMock = new Mock<IShippingDbContext>();
        dbMock.SetupGet(d => d.Shipments).Returns(mockSet.Object);
        dbMock.Setup(d => d.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        return (dbMock, store);
    }

    private static CreateShipment CreateSampleCommand(Guid orderId, string postalCode = "700000") =>
        new(
            orderId,
            "cust-001",
            new ShippingAddressDto("Alice Nguyen", "123 Le Loi", "Ho Chi Minh City", "HC", postalCode, "VN", "+84901234567"),
            [new ShipmentItemDto("SKU-001", "Mechanical Keyboard", 1)]);

    [Fact]
    public async Task Consume_CreateShipment_CreatesShipmentAndPublishesShipmentCreated_AndIsIdempotent()
    {
        var (dbMock, store) = CreateMockDbContext();
        var carrierClient = new MockCarrierProviderClient();
        var consumer = new CreateShipmentConsumer(dbMock.Object, carrierClient, NullLogger<CreateShipmentConsumer>.Instance);

        var orderId = Guid.NewGuid();
        var command = CreateSampleCommand(orderId);
        var publishedEvents = new List<ShipmentCreated>();

        var contextMock = new Mock<ConsumeContext<CreateShipment>>();
        contextMock.SetupGet(c => c.Message).Returns(command);
        contextMock.SetupGet(c => c.CorrelationId).Returns(orderId);
        contextMock.SetupGet(c => c.MessageId).Returns(Guid.NewGuid());
        contextMock.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        contextMock
            .Setup(c => c.Publish(It.IsAny<ShipmentCreated>(), It.IsAny<CancellationToken>()))
            .Callback<ShipmentCreated, CancellationToken>((evt, _) => publishedEvents.Add(evt))
            .Returns(Task.CompletedTask);
        contextMock
            .Setup(c => c.Publish(It.IsAny<MessageEnvelope<ShipmentCreated>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await consumer.Consume(contextMock.Object);

        store.Should().ContainSingle();
        store[0].Status.Should().Be(ShipmentStatus.LabelCreated);
        store[0].TrackingNumber.Should().StartWith("SS-TRK-");
        publishedEvents.Should().ContainSingle(e => e.OrderId == orderId);

        // Second duplicate CreateShipment call for the same OrderId must not create a second shipment
        await consumer.Consume(contextMock.Object);

        store.Should().ContainSingle();
        publishedEvents.Should().HaveCount(2);
    }

    [Fact]
    public async Task Consume_WhenCarrierTimesOut_MarksShipmentUnknown_AndRecoversOnRetryViaQueryStatus()
    {
        var (dbMock, store) = CreateMockDbContext();
        var carrierClient = new MockCarrierProviderClient();
        var consumer = new CreateShipmentConsumer(dbMock.Object, carrierClient, NullLogger<CreateShipmentConsumer>.Instance);

        var orderId = Guid.NewGuid();
        var timeoutCommand = CreateSampleCommand(orderId, postalCode: "00000-TIMEOUT");
        var publishedCreated = new List<ShipmentCreated>();

        var contextMock = new Mock<ConsumeContext<CreateShipment>>();
        contextMock.SetupGet(c => c.Message).Returns(timeoutCommand);
        contextMock.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        contextMock
            .Setup(c => c.Publish(It.IsAny<ShipmentCreated>(), It.IsAny<CancellationToken>()))
            .Callback<ShipmentCreated, CancellationToken>((evt, _) => publishedCreated.Add(evt))
            .Returns(Task.CompletedTask);
        contextMock
            .Setup(c => c.Publish(It.IsAny<MessageEnvelope<ShipmentCreated>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // First attempt times out -> ShipmentUnknown, no ShipmentCreated published yet
        await consumer.Consume(contextMock.Object);

        var shipment = store.Single(s => s.OrderId == orderId);
        shipment.Status.Should().Be(ShipmentStatus.ShipmentUnknown);
        shipment.ExternalShipmentRequestId.Should().NotBeNullOrWhiteSpace();
        publishedCreated.Should().BeEmpty();

        // Retry queries carrier status using stored ExternalShipmentRequestId and transitions to LabelCreated
        await consumer.Consume(contextMock.Object);

        shipment.Status.Should().Be(ShipmentStatus.LabelCreated);
        publishedCreated.Should().ContainSingle(e => e.OrderId == orderId);
    }

    [Fact]
    public async Task Consume_WhenCarrierRejectsAddress_MarksShipmentFailed_AndPublishesShipmentFailed()
    {
        var (dbMock, store) = CreateMockDbContext();
        var carrierClient = new MockCarrierProviderClient();
        var consumer = new CreateShipmentConsumer(dbMock.Object, carrierClient, NullLogger<CreateShipmentConsumer>.Instance);

        var orderId = Guid.NewGuid();
        var failCommand = CreateSampleCommand(orderId, postalCode: "00000-FAIL");
        var publishedFailed = new List<ShipmentFailed>();

        var contextMock = new Mock<ConsumeContext<CreateShipment>>();
        contextMock.SetupGet(c => c.Message).Returns(failCommand);
        contextMock
            .Setup(c => c.Publish(It.IsAny<ShipmentFailed>(), It.IsAny<CancellationToken>()))
            .Callback<ShipmentFailed, CancellationToken>((evt, _) => publishedFailed.Add(evt))
            .Returns(Task.CompletedTask);
        contextMock
            .Setup(c => c.Publish(It.IsAny<MessageEnvelope<ShipmentFailed>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await consumer.Consume(contextMock.Object);

        var shipment = store.Single(s => s.OrderId == orderId);
        shipment.Status.Should().Be(ShipmentStatus.Failed);
        publishedFailed.Should().ContainSingle(e => e.OrderId == orderId);
    }
}
