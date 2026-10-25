using FluentAssertions;
using ShopSphere.Services.Shipping.Domain;
using ShopSphere.Services.Shipping.Domain.Events;
using ShopSphere.Services.Shipping.Domain.Exceptions;

namespace ShopSphere.Services.Shipping.Tests.Unit.Domain;

public class ShipmentTests
{
    private static ShippingAddress CreateSampleAddress() =>
        ShippingAddress.Create("Alice Nguyen", "123 Le Loi", "Ho Chi Minh City", "HC", "700000", "VN", "+84901234567");

    private static List<ShipmentItem> CreateSampleItems(Guid shipmentId) =>
    [
        ShipmentItem.Create(shipmentId, "SKU-001", "Mechanical Keyboard", 1),
        ShipmentItem.Create(shipmentId, "SKU-002", "USB-C Cable", 2)
    ];

    [Fact]
    public void Create_WithValidInput_InitializesPendingShipment()
    {
        var shipmentId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var shipment = Shipment.Create(shipmentId, orderId, "cust-001", CreateSampleAddress(), CreateSampleItems(shipmentId));

        shipment.Id.Should().Be(shipmentId);
        shipment.OrderId.Should().Be(orderId);
        shipment.CustomerId.Should().Be("cust-001");
        shipment.Status.Should().Be(ShipmentStatus.Pending);
        shipment.Items.Should().HaveCount(2);
        shipment.Milestones.Should().ContainSingle(m => m.Status == ShipmentStatus.Pending);
    }

    [Fact]
    public void AssignTrackingLabel_TransitionsToLabelCreated_AndIsIdempotent()
    {
        var shipmentId = Guid.NewGuid();
        var shipment = Shipment.Create(shipmentId, Guid.NewGuid(), "cust-001", CreateSampleAddress(), CreateSampleItems(shipmentId));

        shipment.AssignTrackingLabel("TRK-123456", "REQ-001");

        shipment.Status.Should().Be(ShipmentStatus.LabelCreated);
        shipment.TrackingNumber.Should().Be("TRK-123456");
        shipment.ExternalShipmentRequestId.Should().Be("REQ-001");
        shipment.DomainEvents.OfType<ShipmentCreatedDomainEvent>().Should().ContainSingle();

        // Calling again when already LabelCreated should be a no-op
        shipment.AssignTrackingLabel("TRK-123456", "REQ-001");
        shipment.DomainEvents.OfType<ShipmentCreatedDomainEvent>().Should().ContainSingle();
    }

    [Fact]
    public void MarkUnknown_OnCarrierTimeout_SetsShipmentUnknownAndStoresExternalRequestId()
    {
        var shipmentId = Guid.NewGuid();
        var shipment = Shipment.Create(shipmentId, Guid.NewGuid(), "cust-001", CreateSampleAddress(), CreateSampleItems(shipmentId));

        shipment.MarkUnknown("REQ-TIMEOUT-99", "Carrier HTTP request timed out after 5s");

        shipment.Status.Should().Be(ShipmentStatus.ShipmentUnknown);
        shipment.ExternalShipmentRequestId.Should().Be("REQ-TIMEOUT-99");
        shipment.FailureReason.Should().Contain("timed out");

        // Resolving after querying carrier status transitions to LabelCreated
        shipment.AssignTrackingLabel("TRK-RESOLVED-99", "REQ-TIMEOUT-99");
        shipment.Status.Should().Be(ShipmentStatus.LabelCreated);
        shipment.FailureReason.Should().BeNull();
    }

    [Fact]
    public void RecordCarrierHandover_FromLabelCreated_TransitionsToHandedOver_AndEmitsEvent()
    {
        var shipmentId = Guid.NewGuid();
        var shipment = Shipment.Create(shipmentId, Guid.NewGuid(), "cust-001", CreateSampleAddress(), CreateSampleItems(shipmentId));
        shipment.AssignTrackingLabel("TRK-100", "REQ-100");

        shipment.RecordCarrierHandover("WH-SGN-01");

        shipment.Status.Should().Be(ShipmentStatus.HandedOver);
        shipment.HandedOverAtUtc.Should().NotBeNull();
        shipment.DomainEvents.OfType<ShipmentHandedOverDomainEvent>().Should().ContainSingle();
    }

    [Fact]
    public void RecordInTransit_And_RecordDelivered_FollowValidLifecycle()
    {
        var shipmentId = Guid.NewGuid();
        var shipment = Shipment.Create(shipmentId, Guid.NewGuid(), "cust-001", CreateSampleAddress(), CreateSampleItems(shipmentId));
        shipment.AssignTrackingLabel("TRK-200", "REQ-200");
        shipment.RecordCarrierHandover("WH-SGN-01");

        shipment.RecordInTransit("Arrived at sorting facility", "HUB-SGN-CENTRAL");
        shipment.Status.Should().Be(ShipmentStatus.InTransit);

        shipment.RecordDelivered("Ho Chi Minh City");
        shipment.Status.Should().Be(ShipmentStatus.Delivered);
        shipment.DeliveredAtUtc.Should().NotBeNull();
        shipment.DomainEvents.OfType<ShipmentDeliveredDomainEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Cancel_BeforeCarrierHandover_TransitionsToCancelled()
    {
        var shipmentId = Guid.NewGuid();
        var shipment = Shipment.Create(shipmentId, Guid.NewGuid(), "cust-001", CreateSampleAddress(), CreateSampleItems(shipmentId));
        shipment.AssignTrackingLabel("TRK-300", "REQ-300");

        shipment.Cancel("Customer cancelled order before pickup");

        shipment.Status.Should().Be(ShipmentStatus.Cancelled);
        shipment.FailureReason.Should().Be("Customer cancelled order before pickup");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cancel_AfterCarrierHandover_ThrowsInvalidShipmentStateTransitionException(bool inTransit)
    {
        var shipmentId = Guid.NewGuid();
        var shipment = Shipment.Create(shipmentId, Guid.NewGuid(), "cust-001", CreateSampleAddress(), CreateSampleItems(shipmentId));
        shipment.AssignTrackingLabel("TRK-400", "REQ-400");
        shipment.RecordCarrierHandover("WH-SGN-01");
        if (inTransit)
        {
            shipment.RecordInTransit("Sorting", "HUB-01");
        }

        var act = () => shipment.Cancel("Late cancellation attempt");

        act.Should().Throw<InvalidShipmentStateTransitionException>();
    }
}
