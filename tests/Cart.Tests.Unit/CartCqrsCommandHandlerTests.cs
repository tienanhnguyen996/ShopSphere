using FluentAssertions;
using Moq;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Cart.Application.Carts.Commands.AddItem;
using ShopSphere.Services.Cart.Application.Carts.Commands.ClearCart;
using ShopSphere.Services.Cart.Application.Carts.Commands.RemoveItem;
using ShopSphere.Services.Cart.Application.Carts.Commands.UpdateQuantity;
using ShopSphere.Services.Cart.Application.Carts.Queries.GetCart;
using ShopSphere.Services.Cart.Application.Common.Interfaces;
using ShopSphere.Services.Cart.Domain;
using Xunit;
using CartAggregate = ShopSphere.Services.Cart.Domain.Cart;

namespace ShopSphere.Services.Cart.Tests.Unit;

public class CartCqrsCommandHandlerTests
{
    private readonly Mock<ICartRepository> _cartRepoMock = new();

    [Fact]
    public async Task GetCartQueryHandler_WhenCartExists_ShouldReturnCartDto()
    {
        // Arrange
        var customerId = "cust-10";
        var cart = new CartAggregate(customerId);
        cart.AddItem("ITEM-A", 2);

        _cartRepoMock.Setup(r => r.GetCartAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        var handler = new GetCartQueryHandler(_cartRepoMock.Object, new GetCartQueryValidator());

        // Act
        var result = await handler.HandleAsync(new GetCartQuery(customerId));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CustomerId.Should().Be(customerId);
        result.Value.Items.Should().HaveCount(1);
        result.Value.TotalQuantity.Should().Be(2);
    }

    [Fact]
    public async Task AddItemToCartCommandHandler_WhenValid_ShouldSaveCartAndReturnDto()
    {
        // Arrange
        var customerId = "cust-11";
        var cart = new CartAggregate(customerId);

        _cartRepoMock.Setup(r => r.GetCartAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        var handler = new AddItemToCartCommandHandler(_cartRepoMock.Object, new AddItemToCartCommandValidator());

        // Act
        var result = await handler.HandleAsync(new AddItemToCartCommand(customerId, "SKU-TEST-1", 3));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.TotalQuantity.Should().Be(3);
        _cartRepoMock.Verify(r => r.SaveCartAsync(It.IsAny<CartAggregate>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateCartItemQuantityCommandHandler_WhenSetToZero_ShouldRemoveItem()
    {
        // Arrange
        var customerId = "cust-12";
        var cart = new CartAggregate(customerId);
        cart.AddItem("SKU-TEST-1", 3);

        _cartRepoMock.Setup(r => r.GetCartAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        var handler = new UpdateCartItemQuantityCommandHandler(_cartRepoMock.Object, new UpdateCartItemQuantityCommandValidator());

        // Act
        var result = await handler.HandleAsync(new UpdateCartItemQuantityCommand(customerId, "SKU-TEST-1", 0));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        _cartRepoMock.Verify(r => r.SaveCartAsync(It.IsAny<CartAggregate>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveCartItemCommandHandler_ShouldRemoveItemAndSave()
    {
        // Arrange
        var customerId = "cust-13";
        var cart = new CartAggregate(customerId);
        cart.AddItem("SKU-1", 2);
        cart.AddItem("SKU-2", 1);

        _cartRepoMock.Setup(r => r.GetCartAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        var handler = new RemoveCartItemCommandHandler(_cartRepoMock.Object, new RemoveCartItemCommandValidator());

        // Act
        var result = await handler.HandleAsync(new RemoveCartItemCommand(customerId, "SKU-1"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items.First().Sku.Should().Be("SKU-2");
        _cartRepoMock.Verify(r => r.SaveCartAsync(It.IsAny<CartAggregate>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClearCartCommandHandler_ShouldCallClearAndReturnTrue()
    {
        // Arrange
        var customerId = "cust-14";
        _cartRepoMock.Setup(r => r.ClearCartAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new ClearCartCommandHandler(_cartRepoMock.Object, new ClearCartCommandValidator());

        // Act
        var result = await handler.HandleAsync(new ClearCartCommand(customerId));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        _cartRepoMock.Verify(r => r.ClearCartAsync(customerId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
