using FluentAssertions;
using MassTransit;
using Moq;
using ShopSphere.BuildingBlocks.Messaging.Contracts;
using ShopSphere.Services.Shipping.Application.Abstractions;
using ShopSphere.Services.Shipping.Application.Commands;
using ShopSphere.Services.Shipping.Application.Contracts;
using ShopSphere.Services.Shipping.Domain;
using ShopSphere.Services.Shipping.Domain.Exceptions;
using ShopSphere.Services.Shipping.Tests.Unit.Helpers;

namespace ShopSphere.Services.Shipping.Tests.Unit.Application;

public class CarrierHandoverTests
{
    [Fact]
    public async Task RecordCarrierHandover_TransitionsToHandedOver_PublishesShipmentHandedOver_AndBlocksLateCancellation()
    {
        var shipmentId = Guid.NewGuid();
        var shipment = Shipment.Create(
            shipmentId,
            Guid.NewGuid(),
            "cust-001",
            ShippingAddress.Create("Alice Nguyen", "123 Le Loi", "Ho Chi Minh City", "HC", "700000", "VN", "+84901234567"),
            [ShipmentItem.Create(shipmentId, "SKU-001", "Mechanical Keyboard", 1)]);

        shipment.AssignTrackingLabel("SS-TRK-HANDOVER-1", "REQ-H-1");

        var store = new List<Shipment> { shipment };
        var mockSet = MockDbSetExtensions.CreateMockDbSet(store);
        var dbMock = new Mock<IShippingDbContext>();
        dbMock.SetupGet(d => d.Shipments).Returns(mockSet.Object);
        dbMock.Setup(d => d.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var publishMock = new Mock<IPublishEndpoint>();
        var handedOverEvents = new List<ShipmentHandedOver>();

        publishMock
            .Setup(p => p.Publish(It.IsAny<ShipmentHandedOver>(), It.IsAny<CancellationToken>()))
            .Callback<ShipmentHandedOver, CancellationToken>((evt, _) => handedOverEvents.Add(evt))
            .Returns(Task.CompletedTask);
        publishMock
            .Setup(p => p.Publish(It.IsAny<MessageEnvelope<ShipmentHandedOver>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handoverHandler = new RecordCarrierHandoverCommandHandler(dbMock.Object, publishMock.Object);
        var dto = await handoverHandler.HandleAsync(new RecordCarrierHandoverCommand(shipment.Id, "WH-SGN-01", "Driver picked up parcel"));

        dto.Status.Should().Be(ShipmentStatus.HandedOver.ToString());
        handedOverEvents.Should().ContainSingle(e => e.ShipmentId == shipment.Id && e.TrackingNumber == "SS-TRK-HANDOVER-1");

        // Attempting to cancel after carrier handover must fail with InvalidShipmentStateTransitionException
        var cancelHandler = new CancelShipmentCommandHandler(dbMock.Object);
        var cancelAct = () => cancelHandler.HandleAsync(new CancelShipmentCommand(shipment.Id, "Customer requested cancellation after pickup"));
        await cancelAct.Should().ThrowAsync<InvalidShipmentStateTransitionException>();
    }
}
