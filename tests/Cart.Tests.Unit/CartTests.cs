using FluentAssertions;
using ShopSphere.Services.Cart.Domain;
using ShopSphere.Services.Cart.Domain.Exceptions;
using Xunit;
using CartAggregate = ShopSphere.Services.Cart.Domain.Cart;

namespace ShopSphere.Services.Cart.Tests.Unit;

public class CartTests
{
    [Fact]
    public void Constructor_WithValidCustomerId_ShouldCreateEmptyCart()
    {
        // Arrange & Act
        var cart = new CartAggregate("cust-101");

        // Assert
        cart.CustomerId.Should().Be("cust-101");
        cart.Items.Should().BeEmpty();
        cart.TotalQuantity.Should().Be(0);
        cart.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        cart.LastModifiedUtc.Should().Be(cart.CreatedAtUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidCustomerId_ShouldThrowDomainValidationException(string? invalidId)
    {
        // Act
        var act = () => new CartAggregate(invalidId!);

        // Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*Customer ID cannot be empty*");
    }

    [Fact]
    public void AddItem_WhenItemDoesNotExist_ShouldAddNewItem()
    {
        // Arrange
        var cart = new CartAggregate("cust-101");

        // Act
        cart.AddItem("KB-LOGI-MX", 2);

        // Assert
        cart.Items.Should().HaveCount(1);
        cart.Items.First().Sku.Should().Be("KB-LOGI-MX");
        cart.Items.First().Quantity.Should().Be(2);
        cart.TotalQuantity.Should().Be(2);
    }

    [Fact]
    public void AddItem_WhenItemAlreadyExists_ShouldIncrementQuantity()
    {
        // Arrange
        var cart = new CartAggregate("cust-101");
        cart.AddItem("KB-LOGI-MX", 3);

        // Act
        cart.AddItem("KB-LOGI-MX", 4);

        // Assert
        cart.Items.Should().HaveCount(1);
        cart.Items.First().Quantity.Should().Be(7);
        cart.TotalQuantity.Should().Be(7);
    }

    [Fact]
    public void AddItem_WhenTotalQuantityExceeds99_ShouldThrowDomainValidationException()
    {
        // Arrange
        var cart = new CartAggregate("cust-101");
        cart.AddItem("KB-LOGI-MX", 90);

        // Act
        var act = () => cart.AddItem("KB-LOGI-MX", 10);

        // Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*cannot exceed 99*");
        cart.Items.First().Quantity.Should().Be(90);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100)]
    public void AddItem_WithInvalidQuantity_ShouldThrowDomainValidationException(int quantity)
    {
        // Arrange
        var cart = new CartAggregate("cust-101");

        // Act
        var act = () => cart.AddItem("KB-LOGI-MX", quantity);

        // Assert
        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void UpdateItemQuantity_WhenPositive_ShouldUpdateQuantity()
    {
        // Arrange
        var cart = new CartAggregate("cust-101");
        cart.AddItem("KB-LOGI-MX", 3);

        // Act
        cart.UpdateItemQuantity("KB-LOGI-MX", 5);

        // Assert
        cart.Items.First().Quantity.Should().Be(5);
        cart.TotalQuantity.Should().Be(5);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void UpdateItemQuantity_WhenZeroOrNegative_ShouldRemoveItem(int quantity)
    {
        // Arrange
        var cart = new CartAggregate("cust-101");
        cart.AddItem("KB-LOGI-MX", 3);

        // Act
        cart.UpdateItemQuantity("KB-LOGI-MX", quantity);

        // Assert
        cart.Items.Should().BeEmpty();
        cart.TotalQuantity.Should().Be(0);
    }

    [Fact]
    public void UpdateItemQuantity_WhenQuantityExceeds99_ShouldThrowDomainValidationException()
    {
        // Arrange
        var cart = new CartAggregate("cust-101");
        cart.AddItem("KB-LOGI-MX", 5);

        // Act
        var act = () => cart.UpdateItemQuantity("KB-LOGI-MX", 105);

        // Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*cannot exceed 99*");
    }

    [Fact]
    public void RemoveItem_ShouldRemoveSpecifiedItem()
    {
        // Arrange
        var cart = new CartAggregate("cust-101");
        cart.AddItem("KB-LOGI-MX", 2);
        cart.AddItem("MOU-LOGI-MX", 1);

        // Act
        cart.RemoveItem("KB-LOGI-MX");

        // Assert
        cart.Items.Should().HaveCount(1);
        cart.Items.First().Sku.Should().Be("MOU-LOGI-MX");
    }

    [Fact]
    public void Clear_ShouldRemoveAllItems()
    {
        // Arrange
        var cart = new CartAggregate("cust-101");
        cart.AddItem("KB-LOGI-MX", 2);
        cart.AddItem("MOU-LOGI-MX", 1);

        // Act
        cart.Clear();

        // Assert
        cart.Items.Should().BeEmpty();
        cart.TotalQuantity.Should().Be(0);
    }

    [Fact]
    public void CartItem_WithInvalidSku_ShouldThrowDomainValidationException()
    {
        // Act
        var act = () => new CartItem("invalid sku with spaces!", 1);

        // Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*must contain only uppercase alphanumeric*");
    }

    [Fact]
    public void Serialization_ShouldRoundtripProperly()
    {
        var cart = new CartAggregate("cust-101");
        cart.AddItem("KB-LOGI-MX", 2);
        var json = System.Text.Json.JsonSerializer.Serialize(cart, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<CartAggregate>(json, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        deserialized.Should().NotBeNull();
        deserialized!.Items.Should().HaveCount(1);
    }
}
