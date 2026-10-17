using FluentAssertions;
using ShopSphere.Services.Catalog.Domain;
using ShopSphere.Services.Catalog.Domain.Events;
using ShopSphere.Services.Catalog.Domain.Exceptions;

namespace ShopSphere.Services.Catalog.Tests.Unit;

public class ProductTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldInstantiateProduct()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Mechanical Keyboard";
        var description = "RGB Wireless mechanical keyboard";
        var sku = new Sku("KB-RGB-001");
        var price = new Money(99.99m, "USD");

        // Act
        var product = Product.Create(id, name, description, sku, price);

        // Assert
        product.Id.Should().Be(id);
        product.Name.Should().Be(name);
        product.Description.Should().Be(description);
        product.Sku.Should().Be(sku);
        product.Price.Should().Be(price);
        product.Status.Should().Be(ProductStatus.Draft);
        product.PriceVersion.Should().Be(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_WithInvalidName_ShouldThrowArgumentException(string invalidName)
    {
        // Arrange & Act
        var act = () => Product.Create(
            Guid.NewGuid(),
            invalidName,
            "Description",
            new Sku("SKU-123"),
            new Money(10m, "USD"));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("A")]
    [InlineData("AB")]
    public void Create_WithTooShortSku_ShouldThrowArgumentException(string shortSku)
    {
        // Arrange & Act
        var act = () => new Sku(shortSku);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("SKU@123")]
    [InlineData("SKU 123")]
    [InlineData("SKU#ABC")]
    [InlineData("SKU!")]
    public void Create_WithInvalidSkuCharacters_ShouldThrowArgumentException(string invalidSku)
    {
        // Arrange & Act
        var act = () => new Sku(invalidSku);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*alphanumeric*");
    }

    [Theory]
    [InlineData("PROD-123")]
    [InlineData("ITEM_456")]
    [InlineData("ABC123XYZ")]
    public void Create_WithValidSku_ShouldSucceed(string validSku)
    {
        // Act
        var sku = new Sku(validSku);

        // Assert
        sku.Value.Should().Be(validSku);
    }

    [Fact]
    public void Create_WithNegativePrice_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange & Act
        var act = () => new Money(-5m, "USD");

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_WithZeroPrice_ShouldThrowDomainValidationException()
    {
        // Arrange & Act
        var act = () => Product.Create(
            Guid.NewGuid(),
            "Mechanical Keyboard",
            "Description",
            new Sku("KB-RGB-001"),
            Money.Zero("USD"));

        // Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*greater than zero*");
    }

    [Fact]
    public void UpdatePrice_WithDifferentAmount_ShouldIncrementPriceVersionAndRaiseDomainEvent()
    {
        // Arrange
        var product = Product.Create(
            Guid.NewGuid(),
            "Mechanical Keyboard",
            "Description",
            new Sku("KB-RGB-001"),
            new Money(100m, "USD"));

        var newPrice = new Money(120m, "USD");

        // Act
        product.UpdatePrice(newPrice);

        // Assert
        product.Price.Should().Be(newPrice);
        product.PriceVersion.Should().Be(2);

        product.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ProductPriceChangedDomainEvent>()
            .Which.Should().Match<ProductPriceChangedDomainEvent>(e =>
                e.ProductId == product.Id &&
                e.Sku == product.Sku &&
                e.OldPrice.Amount == 100m &&
                e.NewPrice.Amount == 120m &&
                e.NewPriceVersion == 2);
    }

    [Fact]
    public void UpdatePrice_WithZeroOrNegativeAmount_ShouldThrowDomainValidationException()
    {
        // Arrange
        var product = Product.Create(
            Guid.NewGuid(),
            "Mechanical Keyboard",
            "Description",
            new Sku("KB-RGB-001"),
            new Money(100m, "USD"));

        // Act
        var act = () => product.UpdatePrice(Money.Zero("USD"));

        // Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*greater than zero*");
    }

    [Fact]
    public void UpdatePrice_WithIdenticalAmount_ShouldNotIncrementPriceVersionOrRaiseDomainEvent()
    {
        // Arrange
        var price = new Money(100m, "USD");
        var product = Product.Create(
            Guid.NewGuid(),
            "Mechanical Keyboard",
            "Description",
            new Sku("KB-RGB-001"),
            price);

        // Act
        product.UpdatePrice(new Money(100m, "USD"));

        // Assert
        product.PriceVersion.Should().Be(1);
        product.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Activate_FromDraft_ShouldTransitionToActive()
    {
        // Arrange
        var product = Product.Create(
            Guid.NewGuid(),
            "Mechanical Keyboard",
            "Description",
            new Sku("KB-RGB-001"),
            new Money(100m, "USD"));

        // Act
        product.Activate();

        // Assert
        product.Status.Should().Be(ProductStatus.Active);
    }

    [Fact]
    public void Discontinue_ShouldTransitionToDiscontinued()
    {
        // Arrange
        var product = Product.Create(
            Guid.NewGuid(),
            "Mechanical Keyboard",
            "Description",
            new Sku("KB-RGB-001"),
            new Money(100m, "USD"));
        product.Activate();

        // Act
        product.Discontinue();

        // Assert
        product.Status.Should().Be(ProductStatus.Discontinued);
        product.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Activate_WhenDiscontinued_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var product = Product.Create(
            Guid.NewGuid(),
            "Mechanical Keyboard",
            "Description",
            new Sku("KB-RGB-001"),
            new Money(100m, "USD"));
        product.Discontinue();

        // Act
        var act = () => product.Activate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot be reactivated*");
    }

    [Fact]
    public void UpdatePrice_WhenDiscontinued_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var product = Product.Create(
            Guid.NewGuid(),
            "Mechanical Keyboard",
            "Description",
            new Sku("KB-RGB-001"),
            new Money(100m, "USD"));
        product.Discontinue();

        // Act
        var act = () => product.UpdatePrice(new Money(150m, "USD"));

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*discontinued*");
    }

    [Fact]
    public void Sku_ValueEquality_ShouldWorkAsExpected()
    {
        // Arrange
        var sku1 = new Sku("PROD-001");
        var sku2 = new Sku("prod-001");
        var sku3 = new Sku("PROD-002");

        // Assert
        sku1.Should().Be(sku2);
        sku1.GetHashCode().Should().Be(sku2.GetHashCode());
        sku1.Should().NotBe(sku3);
    }

    [Fact]
    public void Money_ValueEquality_ShouldWorkAsExpected()
    {
        // Arrange
        var m1 = new Money(49.99m, "USD");
        var m2 = new Money(49.99m, "usd");
        var m3 = new Money(50.00m, "USD");

        // Assert
        m1.Should().Be(m2);
        m1.GetHashCode().Should().Be(m2.GetHashCode());
        m1.Should().NotBe(m3);
    }

    [Fact]
    public void Money_ArithmeticOperations_ShouldCalculateCorrectly()
    {
        // Arrange
        var m1 = new Money(50.25m, "USD");
        var m2 = new Money(25.50m, "USD");

        // Act
        var sum = m1 + m2;
        var diff = m1 - m2;
        var multiplied = m1 * 2m;
        var multipliedReverse = 3m * m2;

        // Assert
        sum.Should().Be(new Money(75.75m, "USD"));
        diff.Should().Be(new Money(24.75m, "USD"));
        multiplied.Should().Be(new Money(100.50m, "USD"));
        multipliedReverse.Should().Be(new Money(76.50m, "USD"));
    }

    [Fact]
    public void Money_ArithmeticWithMismatchedCurrency_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var m1 = new Money(50m, "USD");
        var m2 = new Money(50m, "EUR");

        // Act & Assert
        var actAdd = () => _ = m1 + m2;
        actAdd.Should().Throw<InvalidOperationException>()
            .WithMessage("*different currencies*");

        var actSub = () => _ = m1 - m2;
        actSub.Should().Throw<InvalidOperationException>()
            .WithMessage("*different currencies*");
    }

    [Fact]
    public void Money_SubtractLargerAmount_ShouldThrowDomainValidationException()
    {
        // Arrange
        var m1 = new Money(10m, "USD");
        var m2 = new Money(20m, "USD");

        // Act & Assert
        var act = () => _ = m1 - m2;
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*negative*");
    }
}
