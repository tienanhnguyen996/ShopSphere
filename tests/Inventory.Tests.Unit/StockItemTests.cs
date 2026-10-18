using FluentAssertions;
using ShopSphere.Services.Inventory.Domain;
using ShopSphere.Services.Inventory.Domain.Exceptions;
using Xunit;

namespace ShopSphere.Services.Inventory.Tests.Unit;

public class StockItemTests
{
    [Fact]
    public void Create_WithValidParameters_InitializesCorrectly()
    {
        // Act
        var stockItem = StockItem.Create("elem-1234", 100);

        // Assert
        stockItem.Id.Should().NotBeEmpty();
        stockItem.Sku.Should().Be("ELEM-1234");
        stockItem.QuantityAvailable.Should().Be(100);
        stockItem.QuantityHeld.Should().Be(0);
        stockItem.QuantityCommitted.Should().Be(0);
        stockItem.Reservations.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithInvalidSku_ThrowsArgumentException(string? invalidSku)
    {
        // Act
        var act = () => StockItem.Create(invalidSku!, 10);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithNegativeStock_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => StockItem.Create("TEST-SKU", -5);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void HoldStock_DecreasesAvailableAndIncreasesHeld()
    {
        // Arrange
        var stockItem = StockItem.Create("TEST-SKU", 10);
        var orderId = Guid.NewGuid();

        // Act
        var reservation = stockItem.HoldStock(orderId, 4, TimeSpan.FromMinutes(15));

        // Assert
        stockItem.QuantityAvailable.Should().Be(6);
        stockItem.QuantityHeld.Should().Be(4);
        stockItem.QuantityCommitted.Should().Be(0);
        stockItem.Reservations.Should().ContainSingle();

        reservation.OrderId.Should().Be(orderId);
        reservation.Sku.Should().Be("TEST-SKU");
        reservation.Quantity.Should().Be(4);
        reservation.Status.Should().Be(ReservationStatus.Held);
        reservation.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void HoldStock_WhenInsufficientStock_ThrowsInsufficientStockDomainException()
    {
        // Arrange
        var stockItem = StockItem.Create("TEST-SKU", 3);
        var orderId = Guid.NewGuid();

        // Act
        var act = () => stockItem.HoldStock(orderId, 5, TimeSpan.FromMinutes(15));

        // Assert
        var ex = act.Should().Throw<InsufficientStockDomainException>().Which;
        ex.Sku.Should().Be("TEST-SKU");
        ex.RequestedQuantity.Should().Be(5);
        ex.AvailableQuantity.Should().Be(3);

        stockItem.QuantityAvailable.Should().Be(3);
        stockItem.QuantityHeld.Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void HoldStock_WithInvalidQuantity_ThrowsArgumentOutOfRangeException(int invalidQty)
    {
        // Arrange
        var stockItem = StockItem.Create("TEST-SKU", 10);

        // Act
        var act = () => stockItem.HoldStock(Guid.NewGuid(), invalidQty, TimeSpan.FromMinutes(10));

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CommitHold_ValidReservation_DecreasesHeldAndIncreasesCommitted()
    {
        // Arrange
        var stockItem = StockItem.Create("TEST-SKU", 20);
        var reservation = stockItem.HoldStock(Guid.NewGuid(), 5, TimeSpan.FromMinutes(15));

        // Act
        var committed = stockItem.CommitHold(reservation.Id);

        // Assert
        committed.Status.Should().Be(ReservationStatus.Committed);
        stockItem.QuantityAvailable.Should().Be(15);
        stockItem.QuantityHeld.Should().Be(0);
        stockItem.QuantityCommitted.Should().Be(5);
    }

    [Fact]
    public void CommitHold_WhenAlreadyCommitted_IsIdempotent()
    {
        // Arrange
        var stockItem = StockItem.Create("TEST-SKU", 20);
        var reservation = stockItem.HoldStock(Guid.NewGuid(), 5, TimeSpan.FromMinutes(15));
        stockItem.CommitHold(reservation.Id);

        // Act
        var secondCommit = stockItem.CommitHold(reservation.Id);

        // Assert
        secondCommit.Status.Should().Be(ReservationStatus.Committed);
        stockItem.QuantityHeld.Should().Be(0);
        stockItem.QuantityCommitted.Should().Be(5);
    }

    [Fact]
    public void CommitHold_WhenExpired_ThrowsReservationExpiredDomainExceptionAndRestoresStock()
    {
        // Arrange
        var stockItem = StockItem.Create("TEST-SKU", 20);
        var reservation = stockItem.HoldStock(Guid.NewGuid(), 5, TimeSpan.FromMinutes(15));
        var simulatedNow = DateTime.UtcNow.AddMinutes(20);

        // Act
        var act = () => stockItem.CommitHold(reservation.Id, simulatedNow);

        // Assert
        var ex = act.Should().Throw<ReservationExpiredDomainException>().Which;
        ex.ReservationId.Should().Be(reservation.Id);

        reservation.Status.Should().Be(ReservationStatus.Expired);
        stockItem.QuantityHeld.Should().Be(0);
        stockItem.QuantityAvailable.Should().Be(20); // restored back
        stockItem.QuantityCommitted.Should().Be(0);
    }

    [Fact]
    public void ReleaseHold_ValidHeldReservation_DecreasesHeldAndIncreasesAvailable()
    {
        // Arrange
        var stockItem = StockItem.Create("TEST-SKU", 20);
        var reservation = stockItem.HoldStock(Guid.NewGuid(), 8, TimeSpan.FromMinutes(15));

        // Act
        var released = stockItem.ReleaseHold(reservation.Id, "Payment declined");

        // Assert
        released.Status.Should().Be(ReservationStatus.Released);
        stockItem.QuantityHeld.Should().Be(0);
        stockItem.QuantityAvailable.Should().Be(20);
        stockItem.QuantityCommitted.Should().Be(0);
    }

    [Fact]
    public void ReleaseHold_WhenAlreadyReleasedOrExpired_IsIdempotent()
    {
        // Arrange
        var stockItem = StockItem.Create("TEST-SKU", 20);
        var reservation = stockItem.HoldStock(Guid.NewGuid(), 8, TimeSpan.FromMinutes(15));
        stockItem.ReleaseHold(reservation.Id, "First release");

        // Act
        var secondRelease = stockItem.ReleaseHold(reservation.Id, "Second release");

        // Assert
        secondRelease.Status.Should().Be(ReservationStatus.Released);
        stockItem.QuantityAvailable.Should().Be(20);
    }

    [Fact]
    public void ReleaseHold_WhenAlreadyCommitted_ThrowsInvalidOperationException()
    {
        // Arrange
        var stockItem = StockItem.Create("TEST-SKU", 20);
        var reservation = stockItem.HoldStock(Guid.NewGuid(), 8, TimeSpan.FromMinutes(15));
        stockItem.CommitHold(reservation.Id);

        // Act
        var act = () => stockItem.ReleaseHold(reservation.Id, "Late cancel");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot release committed*");
    }

    [Fact]
    public void ExpireHold_ValidReservation_RestoresAvailableAndMarksExpired()
    {
        // Arrange
        var stockItem = StockItem.Create("TEST-SKU", 50);
        var reservation = stockItem.HoldStock(Guid.NewGuid(), 10, TimeSpan.FromMinutes(10));

        // Act
        var expired = stockItem.ExpireHold(reservation.Id);

        // Assert
        expired.Status.Should().Be(ReservationStatus.Expired);
        stockItem.QuantityHeld.Should().Be(0);
        stockItem.QuantityAvailable.Should().Be(50);
    }

    [Fact]
    public void AdjustStock_PositiveQuantity_IncreasesAvailable()
    {
        // Arrange
        var stockItem = StockItem.Create("TEST-SKU", 10);

        // Act
        stockItem.AdjustStock(15, "Restock");

        // Assert
        stockItem.QuantityAvailable.Should().Be(25);
    }

    [Fact]
    public void AdjustStock_NegativeQuantity_WithinAvailable_DecreasesAvailable()
    {
        // Arrange
        var stockItem = StockItem.Create("TEST-SKU", 20);

        // Act
        stockItem.AdjustStock(-5, "Damaged stock");

        // Assert
        stockItem.QuantityAvailable.Should().Be(15);
    }

    [Fact]
    public void AdjustStock_NegativeQuantity_ExceedingAvailable_ThrowsInvalidOperationException()
    {
        // Arrange
        var stockItem = StockItem.Create("TEST-SKU", 10);

        // Act
        var act = () => stockItem.AdjustStock(-15, "Audit shrinkage");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*would exceed available quantity*");
    }
}
