using ShopSphere.Services.Catalog.Domain;
using ShopSphere.Services.Catalog.Domain.Events;

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

    [Fact]
    public void Create_WithNegativePrice_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange & Act
        var act = () => new Money(-5m, "USD");

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
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
}
